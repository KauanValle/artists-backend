using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ArtistPlatform.Infrastructure.Services;

/// <summary>Implementação do Web Push Protocol (RFC 8030 + 8291 + VAPID RFC 8292) com .NET nativo.</summary>
public static class WebPushProtocol
{
    public record Resultado(bool Sucesso, HttpStatusCode? Status, string? Erro);

    private const int TamanhoRegistro = 4096; // rs do cabeçalho aes128gcm (RFC 8188)

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

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
        try
        {
            // ── Chaves do navegador (user agent) ──
            var chavePublicaUsuario = Base64UrlDecode(p256dhBase64Url);
            var authSecret = Base64UrlDecode(authBase64Url);
            if (chavePublicaUsuario.Length != 65 || chavePublicaUsuario[0] != 0x04)
                return new Resultado(false, HttpStatusCode.BadRequest, "chave p256dh inválida");

            // ── VAPID: JWT assinado com ES256 usando a chave privada ──
            var chavePrivadaVapid = Base64UrlDecode(vapidPrivateKeyB64Url);
            var chavePublicaVapid = Base64UrlDecode(vapidPublicKeyB64Url);
            if (chavePrivadaVapid.Length != 32 || chavePublicaVapid.Length != 65 || chavePublicaVapid[0] != 0x04)
                return new Resultado(false, null, "chaves VAPID inválidas");

            // O .NET exige o ponto público (Q) junto com D ao importar uma chave EC.
            using var ecdsaVapid = ECDsa.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                D = chavePrivadaVapid,
                Q = new ECPoint { X = chavePublicaVapid[1..33], Y = chavePublicaVapid[33..65] }
            });

            var cabecalho = Base64UrlEncode(Encoding.UTF8.GetBytes("{\"typ\":\"JWT\",\"alg\":\"ES256\"}"));
            var audiencia = new Uri(endpoint).GetLeftPart(UriPartial.Authority);
            var claims = Base64UrlEncode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
            {
                aud = audiencia,
                exp = DateTimeOffset.UtcNow.AddHours(12).ToUnixTimeSeconds(),
                sub = vapidSubject
            })));
            var assinado = $"{cabecalho}.{claims}";
            // SignData no .NET já devolve r||s (64 bytes, IEEE P1363) — formato exigido pelo JWT ES256.
            var assinatura = ecdsaVapid.SignData(Encoding.UTF8.GetBytes(assinado), HashAlgorithmName.SHA256);
            var jwt = $"{assinado}.{Base64UrlEncode(assinatura)}";

            // ── Criptografia do payload (RFC 8291, aes128gcm) ──
            using var usuarioEcdh = ECDiffieHellman.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = chavePublicaUsuario[1..33], Y = chavePublicaUsuario[33..65] }
            });
            using var efemera = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
            var parametrosEfemera = efemera.ExportParameters(false);
            var chavePublicaServidor = new byte[65];
            chavePublicaServidor[0] = 0x04;
            parametrosEfemera.Q.X!.CopyTo(chavePublicaServidor, 1);
            parametrosEfemera.Q.Y!.CopyTo(chavePublicaServidor, 33);

            var segredoCompartilhado = efemera.DeriveRawSecretAgreement(usuarioEcdh.PublicKey);

            // RFC 8291 §3.4: IKM = HKDF(salt=auth_secret, ikm=ecdh, info="WebPush: info\0" || ua_public || as_public)
            var infoChave = Concatenar(
                Encoding.UTF8.GetBytes("WebPush: info\0"),
                chavePublicaUsuario,
                chavePublicaServidor);
            var ikm = HKDF.DeriveKey(HashAlgorithmName.SHA256, segredoCompartilhado, 32, authSecret, infoChave);

            var salt = RandomNumberGenerator.GetBytes(16);
            var cek = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 16, salt, Encoding.UTF8.GetBytes("Content-Encoding: aes128gcm\0"));
            var nonce = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 12, salt, Encoding.UTF8.GetBytes("Content-Encoding: nonce\0"));

            // registro único: payload + delimitador 0x02 (último registro)
            var textoPayload = Encoding.UTF8.GetBytes(payloadJson);
            var textoAberto = new byte[textoPayload.Length + 1];
            textoPayload.CopyTo(textoAberto, 0);
            textoAberto[^1] = 0x02;

            using var aes = new AesGcm(cek, 16);
            var cifrado = new byte[textoAberto.Length];
            var tag = new byte[16];
            aes.Encrypt(nonce, textoAberto, cifrado, tag);

            // Cabeçalho aes128gcm (RFC 8188): salt(16) | rs(4) | idlen(1) | keyid (= chave pública do servidor, 65)
            var corpo = new byte[16 + 4 + 1 + chavePublicaServidor.Length + cifrado.Length + tag.Length];
            salt.CopyTo(corpo, 0);
            corpo[16] = (byte)(TamanhoRegistro >> 24);
            corpo[17] = (byte)(TamanhoRegistro >> 16);
            corpo[18] = (byte)(TamanhoRegistro >> 8);
            corpo[19] = (byte)TamanhoRegistro;
            corpo[20] = (byte)chavePublicaServidor.Length;
            chavePublicaServidor.CopyTo(corpo, 21);
            cifrado.CopyTo(corpo, 21 + chavePublicaServidor.Length);
            tag.CopyTo(corpo, 21 + chavePublicaServidor.Length + cifrado.Length);

            // ── Envio ──
            using var requisicao = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new ByteArrayContent(corpo)
            };
            requisicao.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            requisicao.Content.Headers.ContentEncoding.Add("aes128gcm");
            // TTL e Urgency são cabeçalhos da requisição (não do conteúdo).
            requisicao.Headers.TryAddWithoutValidation("TTL", "2419200"); // 28 dias
            requisicao.Headers.TryAddWithoutValidation("Urgency", "normal");
            requisicao.Headers.TryAddWithoutValidation("Authorization", $"vapid t={jwt}, k={Base64UrlEncode(chavePublicaVapid)}");

            using var resposta = await Http.SendAsync(requisicao);
            if (resposta.IsSuccessStatusCode)
                return new Resultado(true, resposta.StatusCode, null);

            return new Resultado(false, resposta.StatusCode, await resposta.Content.ReadAsStringAsync());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or CryptographicException or FormatException or UriFormatException)
        {
            return new Resultado(false, null, ex.Message);
        }
    }

    private static byte[] Concatenar(params byte[][] partes)
    {
        var resultado = new byte[partes.Sum(p => p.Length)];
        var posicao = 0;
        foreach (var parte in partes)
        {
            parte.CopyTo(resultado, posicao);
            posicao += parte.Length;
        }
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
