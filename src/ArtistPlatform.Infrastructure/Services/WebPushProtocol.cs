using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ArtistPlatform.Infrastructure.Services;

/// <summary>Implementação do Web Push Protocol (RFC 8030 + 8291 + VAPID RFC 8292) com .NET nativo.</summary>
public static class WebPushProtocol
{
    public record Resultado(bool Sucesso, HttpStatusCode? Status, string? Erro);

    /// <summary>Criptografa o payload (aes128gcm), assina o VAPID (ES256) e entrega no endpoint do push service.</summary>
    public static async Task<Resultado> EnviarAsync(
        string endpoint,
        string p256dhBase64Url,
        string authBase64Url,
        string payloadJson,
        string vapidSubject,
        string vapidPublicKeyB64Url,
        string vapidPrivateKeyB64Url)
    {
        // ── VAPID: JWT assinado com ES256 usando a chave privada ──
        var chavePrivada = Base64UrlDecode(vapidPrivateKeyB64Url);
        var ecdsaVapid = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = chavePrivada
        });

        var agora = DateTimeOffset.UtcNow;
        var cabecalho = Base64UrlEncode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { typ = "JWT", alg = "ES256" })));
        var audiencia = new Uri(endpoint).GetLeftPart(UriPartial.Authority);
        var claims = Base64UrlEncode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            aud = audiencia,
            exp = agora.AddHours(12).ToUnixTimeSeconds(),
            sub = vapidSubject
        })));
        var assinado = $"{cabecalho}.{claims}";
        var assinaturaDer = ecdsaVapid.SignData(Encoding.UTF8.GetBytes(assinado), HashAlgorithmName.SHA256);
        var assinatura = DerParaFixo(assinaturaDer);
        var jwt = $"{assinado}.{Base64UrlEncode(assinatura)}";
        var chavePublicaVapid = Base64UrlDecode(vapidPublicKeyB64Url);

        // ── Criptografia do payload (RFC 8291, aes128gcm) ──
        var chavePublicaUsuario = Base64UrlDecode(p256dhBase64Url);
        var authSecret = Base64UrlDecode(authBase64Url);
        if (chavePublicaUsuario.Length != 65 || chavePublicaUsuario[0] != 0x04)
            return new Resultado(false, HttpStatusCode.BadRequest, "chave p256dh inválida");

        var usuarioEcdh = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = { X = chavePublicaUsuario[1..33], Y = chavePublicaUsuario[33..65] }
        });
        var efemera = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var segredoCompartilhado = efemera.DeriveRawSecretAgreement(usuarioEcdh.PublicKey);

        // RFC 8291 §3.3: com auth_secret, o IKM é HKDF-Extract(auth_secret, segredo)
        var ikm = HKDF.DeriveKey(HashAlgorithmName.SHA256, segredoCompartilhado, 32, authSecret, Array.Empty<byte>());
        var salt = RandomNumberGenerator.GetBytes(16);
        var cek = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 16, salt, Encoding.UTF8.GetBytes("Content-Encoding: aes128gcm\u0000"));
        var nonce = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 12, salt, Encoding.UTF8.GetBytes("Content-Encoding: nonce\u0000"));

        // registro único: payload + delimitador de último registro ()
        var textoPayload = Encoding.UTF8.GetBytes(payloadJson);
        var textoAberto = new byte[textoPayload.Length + 1];
        textoPayload.CopyTo(textoAberto, 0);
        textoAberto[^1] = 0x02;

        using var aes = new AesGcm(cek, 16);
        var cifrado = new byte[textoAberto.Length];
        var tag = new byte[16];
        aes.Encrypt(nonce, textoAberto, cifrado, tag);

        var corpo = new byte[16 + 4 + cifrado.Length + tag.Length];
        salt.CopyTo(corpo, 0);
        var tamanho = (uint)(cifrado.Length + tag.Length);
        corpo[16] = (byte)(tamanho >> 24);
        corpo[17] = (byte)(tamanho >> 16);
        corpo[18] = (byte)(tamanho >> 8);
        corpo[19] = (byte)tamanho;
        cifrado.CopyTo(corpo, 20);
        tag.CopyTo(corpo, 20 + cifrado.Length);

        // ── Envio ──
        using var httpClient = new HttpClient();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new ByteArrayContent(corpo)
        };
        requisicao.Content.Headers.Add("Content-Encoding", "aes128gcm");
        requisicao.Content.Headers.Add("Content-Type", "application/octet-stream");
        requisicao.Content.Headers.Add("TTL", "2419200"); // 28 dias
        requisicao.Headers.TryAddWithoutValidation("Authorization", $"vapid t={jwt}, k={Base64UrlEncode(chavePublicaVapid)}");

        var resposta = await httpClient.SendAsync(requisicao);
        if (resposta.IsSuccessStatusCode)
            return new Resultado(true, resposta.StatusCode, null);

        return new Resultado(false, resposta.StatusCode, await resposta.Content.ReadAsStringAsync());
    }

    /// <summary>Converte assinatura ECDSA em formato DER para o formato bruto r||s de 64 bytes (ES256).</summary>
    private static byte[] DerParaFixo(byte[] der)
    {
        var i = 2;
        var rLen = der[i + 1];
        var r = der[(i + 2)..(i + 2 + rLen)];
        i += 2 + rLen;
        var sLen = der[i + 1];
        var s = der[(i + 2)..(i + 2 + sLen)];

        var rFixo = new byte[32];
        var sFixo = new byte[32];
        r.CopyTo(rFixo[^r.Length..]);
        s.CopyTo(sFixo[^s.Length..]);
        var resultado = new byte[64];
        rFixo.CopyTo(resultado, 0);
        sFixo.CopyTo(resultado, 32);
        return resultado;
    }

    public static byte[] Base64UrlDecode(string valor)
    {
        var base64 = valor.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }

    public static string Base64UrlEncode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
