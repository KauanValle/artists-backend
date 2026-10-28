using System.Security.Cryptography;
using System.Text;
using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Persistence;
using ArtistPlatform.Domain.Common;
using ArtistPlatform.Domain.Entities;
using ArtistPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Application.Services;

public interface IContractService
{
    /// <summary>Rascunho auto-preenchido ou contrato já assinado da contratação.</summary>
    Task<ArtistContractDto> GetForBookingAsync(Guid bookingId);

    /// <summary>Assina o contrato com evidências (hash do texto, IP, user-agent, data/hora UTC).</summary>
    Task<ArtistContractDto> SignAsync(Guid bookingId, SignArtistContractRequest request, string? ip, string? userAgent);
}

public class ContractService(IAppDbContext db, ICurrentUserService currentUser, IDateTimeClock clock) : IContractService
{
    public async Task<ArtistContractDto> GetForBookingAsync(Guid bookingId)
    {
        var booking = await LoadBookingAsync(bookingId);
        EnsureAccess(booking);

        var existing = await db.SignatureContracts.AsNoTracking()
            .FirstOrDefaultAsync(c => c.BookingRequestId == bookingId);

        var artist = await db.Artists.AsNoTracking().FirstAsync(a => a.Id == booking.ArtistId);
        var (contractorName, contractorContact) = await ResolveContractorAsync(booking);

        var data = new ArtistContractTextBuilder.ContractData(
            artist.ArtisticName,
            existing?.ArtistDocument ?? string.Empty,
            existing?.ArtistAddress ?? string.Empty,
            existing?.ArtistContact ?? artist.Phone,
            contractorName,
            existing?.ContractorDocument ?? string.Empty,
            existing?.ContractorRepresentative ?? string.Empty,
            existing?.ContractorAddress ?? string.Empty,
            contractorContact,
            existing?.PaymentTerms ?? string.Empty,
            FormatDate(booking.EventDate),
            booking.StartTime.ToString(@"hh\:mm"),
            DurationLabel(booking.StartTime, booking.EndTime),
            booking.Location,
            booking.Budget.HasValue ? FormatMoney(booking.Budget.Value) : string.Empty);

        var text = ArtistContractTextBuilder.Build(data);
        var hash = Hash(text);

        return new ArtistContractDto(
            bookingId,
            Signed: existing is not null,
            ContractText: existing?.ContractText ?? text,
            ContentHash: existing?.ContentHash ?? hash,
            ArtistName: artist.Name,
            ArtisticName: artist.ArtisticName,
            ArtistDocument: data.ArtistDocument,
            ArtistAddress: data.ArtistAddress,
            ArtistContact: data.ArtistContact,
            ContractorName: data.ContractorName,
            ContractorDocument: data.ContractorDocument,
            ContractorRepresentative: data.ContractorRepresentative,
            ContractorAddress: data.ContractorAddress,
            ContractorContact: data.ContractorContact,
            PaymentTerms: data.PaymentTerms,
            EventDate: booking.EventDate.ToString("yyyy-MM-dd"),
            StartTime: booking.StartTime.ToString(@"hh\:mm"),
            EndTime: booking.EndTime.ToString(@"hh\:mm"),
            Location: booking.Location,
            CacheValue: booking.Budget,
            SignatureData: existing?.ArtistSignatureData,
            SignedName: existing?.ArtistSignedName,
            SignedAtUtc: existing?.ArtistSignedAtUtc,
            SignedIp: existing?.ArtistIp,
            SignedUserAgent: existing?.ArtistUserAgent);
    }

    public async Task<ArtistContractDto> SignAsync(Guid bookingId, SignArtistContractRequest request, string? ip, string? userAgent)
    {
        var booking = await LoadBookingAsync(bookingId);
        var artistId = currentUser.EnsureArtist();
        if (booking.ArtistId != artistId && currentUser.Role != UserRole.Admin)
            throw new ForbiddenException("Apenas o artista responsável pode assinar o contrato.");
        if (booking.Status is not (BookingStatus.Confirmed or BookingStatus.Completed))
            throw new BusinessRuleException("O contrato é gerado para contratações confirmadas.");

        if (string.IsNullOrWhiteSpace(request.ArtistDocument))
            throw new ValidationException("Informe o CPF/CNPJ do artista.");
        if (string.IsNullOrWhiteSpace(request.ArtistAddress))
            throw new ValidationException("Informe o endereço do artista.");
        if (string.IsNullOrWhiteSpace(request.ArtistContact))
            throw new ValidationException("Informe o telefone/e-mail do artista.");
        if (string.IsNullOrWhiteSpace(request.PaymentTerms))
            throw new ValidationException("Informe a forma e o prazo de pagamento.");
        if (string.IsNullOrWhiteSpace(request.SignatureData) || !request.SignatureData.StartsWith("data:image/png;base64,", StringComparison.Ordinal))
            throw new ValidationException("Assinatura inválida: desenhe sua assinatura antes de salvar.");
        if (request.SignatureData.Length > 500_000)
            throw new ValidationException("Assinatura muito grande. Desenhe novamente.");

        var artist = await db.Artists.AsNoTracking().FirstAsync(a => a.Id == booking.ArtistId);
        var (contractorName, contractorContact) = await ResolveContractorAsync(booking);

        var data = new ArtistContractTextBuilder.ContractData(
            artist.ArtisticName,
            request.ArtistDocument.Trim(),
            request.ArtistAddress.Trim(),
            request.ArtistContact.Trim(),
            contractorName,
            request.ContractorDocument?.Trim() ?? string.Empty,
            request.ContractorRepresentative?.Trim() ?? string.Empty,
            request.ContractorAddress?.Trim() ?? string.Empty,
            request.ContractorContact?.Trim() ?? string.Empty,
            request.PaymentTerms.Trim(),
            FormatDate(booking.EventDate),
            booking.StartTime.ToString(@"hh\:mm"),
            DurationLabel(booking.StartTime, booking.EndTime),
            booking.Location,
            booking.Budget.HasValue ? FormatMoney(booking.Budget.Value) : string.Empty);

        var text = ArtistContractTextBuilder.Build(data);
        var hash = Hash(text);

        var existing = await db.SignatureContracts.FirstOrDefaultAsync(c => c.BookingRequestId == bookingId);
        if (existing is null)
        {
            existing = new SignatureContract
            {
                BookingRequestId = bookingId,
                ArtistId = booking.ArtistId,
                ContractorId = booking.ContractorId
            };
            db.SignatureContracts.Add(existing);
        }

        existing.ContractText = text;
        existing.ContentHash = hash;
        existing.ArtistName = artist.Name;
        existing.ArtistDocument = data.ArtistDocument;
        existing.ArtistAddress = data.ArtistAddress;
        existing.ArtistContact = data.ArtistContact;
        existing.ContractorName = data.ContractorName;
        existing.ContractorDocument = data.ContractorDocument;
        existing.ContractorRepresentative = data.ContractorRepresentative;
        existing.ContractorAddress = data.ContractorAddress;
        existing.ContractorContact = data.ContractorContact;
        existing.PaymentTerms = data.PaymentTerms;
        existing.ArtistSignatureData = request.SignatureData;
        existing.ArtistSignedName = artist.Name;
        existing.ArtistSignedAtUtc = clock.UtcNow;
        existing.ArtistIp = ip;
        existing.ArtistUserAgent = userAgent;
        await db.SaveChangesAsync();

        return new ArtistContractDto(
            bookingId,
            Signed: true,
            ContractText: text,
            ContentHash: hash,
            ArtistName: artist.Name,
            ArtisticName: artist.ArtisticName,
            ArtistDocument: data.ArtistDocument,
            ArtistAddress: data.ArtistAddress,
            ArtistContact: data.ArtistContact,
            ContractorName: data.ContractorName,
            ContractorDocument: data.ContractorDocument,
            ContractorRepresentative: data.ContractorRepresentative,
            ContractorAddress: data.ContractorAddress,
            ContractorContact: data.ContractorContact,
            PaymentTerms: data.PaymentTerms,
            EventDate: booking.EventDate.ToString("yyyy-MM-dd"),
            StartTime: booking.StartTime.ToString(@"hh\:mm"),
            EndTime: booking.EndTime.ToString(@"hh\:mm"),
            Location: booking.Location,
            CacheValue: booking.Budget,
            SignatureData: request.SignatureData,
            SignedName: artist.Name,
            SignedAtUtc: clock.UtcNow,
            SignedIp: ip,
            SignedUserAgent: userAgent);
    }

    private async Task<BookingRequest> LoadBookingAsync(Guid bookingId) =>
        await db.BookingRequests.AsNoTracking().FirstOrDefaultAsync(b => b.Id == bookingId)
        ?? throw new NotFoundException("Solicitação não encontrada.");

    private void EnsureAccess(BookingRequest booking)
    {
        var allowed =
            (currentUser.Role == UserRole.Artist && booking.ArtistId == currentUser.ArtistId) ||
            (currentUser.Role == UserRole.Contractor && booking.ContractorId == currentUser.ContractorId) ||
            currentUser.Role == UserRole.Admin;
        if (!allowed)
            throw new ForbiddenException();
    }

    private async Task<(string Name, string Contact)> ResolveContractorAsync(BookingRequest booking)
    {
        if (booking.IsGuest)
            return (booking.GuestName, booking.GuestPhone);

        var contractor = await db.Contractors.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == booking.ContractorId);
        return (contractor?.Name ?? "Contratante", contractor?.Phone ?? string.Empty);
    }

    private static string FormatDate(DateOnly date) => date.ToString("yyyy-MM-dd");

    private static string DurationLabel(TimeOnly start, TimeOnly end)
    {
        var minutes = (int)(end - start).TotalMinutes;
        var hours = minutes / 60;
        var rest = minutes % 60;
        if (hours == 0) return $"{rest} minutos";
        return rest == 0
            ? hours == 1 ? "1 hora" : $"{hours} horas"
            : $"{hours}h{rest:00}";
    }

    private static string FormatMoney(decimal value) => value.ToString("C", new System.Globalization.CultureInfo("pt-BR"));

    private static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}

/// <summary>Monta o texto integral do contrato com os dados preenchidos (underscores quando vazio).</summary>
public static class ArtistContractTextBuilder
{
    public sealed record ContractData(
        string ArtisticName,
        string ArtistDocument,
        string ArtistAddress,
        string ArtistContact,
        string ContractorName,
        string ContractorDocument,
        string ContractorRepresentative,
        string ContractorAddress,
        string ContractorContact,
        string PaymentTerms,
        string EventDate,
        string StartTime,
        string Duration,
        string Location,
        string CacheValue);

    public static string V(string? value, string fallback = "__________________________________________") =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    public static string Build(ContractData d)
    {
        var line = "__________________________________________";
        return $"""
CONTRATO DE PRESTAÇÃO DE SERVIÇOS ARTÍSTICOS PARA REALIZAÇÃO DE SHOW OU EVENTO

Pelo presente instrumento particular, de um lado:

CONTRATADO(A) / ARTISTA: {V(d.ArtisticName)}
CPF/CNPJ: {V(d.ArtistDocument)}
Endereço: {V(d.ArtistAddress)}
Telefone/E-mail: {V(d.ArtistContact)}

E, de outro lado:

CONTRATANTE / ESTABELECIMENTO: {V(d.ContractorName)}
CPF/CNPJ: {V(d.ContractorDocument)}
Representante legal, se aplicável: {V(d.ContractorRepresentative)}
Endereço: {V(d.ContractorAddress)}
Telefone/E-mail: {V(d.ContractorContact)}

As partes acima identificadas resolvem celebrar o presente Contrato de Prestação de Serviços Artísticos, mediante as cláusulas e condições seguintes.

CLÁUSULA 1ª — DO OBJETO

1.1. O presente contrato tem por objeto a prestação de serviços artísticos pelo(a) CONTRATADO(A), para realização de apresentação musical ou artística no estabelecimento ou evento organizado pelo(a) CONTRATANTE.

1.2. A apresentação será realizada conforme as seguintes informações:

- Data do evento: {d.EventDate}
- Horário de início: {d.StartTime}
- Duração da apresentação: {V(d.Duration, "a combinar")}
- Local do evento: {V(d.Location)}
- Valor total do cachê: {V(d.CacheValue)}
- Forma e prazo de pagamento: {V(d.PaymentTerms)}

1.3. A confirmação da contratação poderá ser comprovada por este instrumento, mensagens eletrônicas, e-mails, comprovantes ou outros meios legalmente admitidos, sem prejuízo da assinatura deste contrato.

CLÁUSULA 2ª — DA CONFIRMAÇÃO E DO COMPROMISSO DAS PARTES

2.1. Após a confirmação da contratação, ambas as partes comprometem-se a cumprir as obrigações assumidas neste instrumento.

2.2. O(A) CONTRATADO(A) compromete-se a comparecer ao local e realizar a apresentação na data e no horário acordados, observadas as condições contratadas e ressalvadas as hipóteses de caso fortuito, força maior ou outros impedimentos legalmente reconhecidos.

2.3. O(A) CONTRATANTE compromete-se a manter a contratação confirmada, disponibilizar as condições necessárias à realização do evento e efetuar o pagamento do cachê nos valores e prazos acordados.

2.4. O(A) CONTRATANTE deverá assegurar as condições de acesso ao local, energia elétrica, estrutura e equipamentos previamente acordados, bem como as autorizações e licenças sob sua responsabilidade.

CLÁUSULA 3ª — DO CANCELAMENTO PELO CONTRATANTE

3.1. O(A) CONTRATANTE poderá solicitar o cancelamento da apresentação sem incidência da multa prevista na cláusula 3.2, desde que comunique formalmente o cancelamento em até 24 (vinte e quatro) horas corridas após a confirmação da contratação, ressalvadas despesas já assumidas, valores antecipados e outras obrigações legalmente devidas.

3.2. Caso o cancelamento seja solicitado após o prazo previsto na cláusula 3.1, o(a) CONTRATANTE ficará sujeito(a) ao pagamento de multa compensatória correspondente a 50% (cinquenta por cento) do valor total do cachê contratado, em razão da reserva da data, da indisponibilidade da agenda artística e dos prejuízos decorrentes da desistência.

3.3. A multa prevista nesta cláusula deverá observar a legislação aplicável, inclusive a possibilidade de redução judicial caso seja considerada manifestamente excessiva ou diante do cumprimento parcial da obrigação.

3.4. Se o cancelamento ocorrer por motivo de caso fortuito ou força maior devidamente comprovado, as partes deverão buscar uma solução de boa-fé, podendo acordar o reagendamento do evento ou a apuração dos valores efetivamente devidos, conforme a legislação aplicável.

3.5. O pedido de cancelamento deverá ser realizado por escrito, por meio de mensagem eletrônica, e-mail ou outro canal que permita comprovar a data e o horário da comunicação.

CLÁUSULA 4ª — DO CANCELAMENTO OU DESCUMPRIMENTO PELO CONTRATADO

4.1. O(A) CONTRATADO(A) compromete-se a comparecer e realizar a apresentação conforme as condições acordadas.

4.2. Caso o(a) CONTRATADO(A), sem justificativa legalmente admissível, deixe de realizar a apresentação, deverá comunicar imediatamente o(a) CONTRATANTE e buscar uma solução consensual, como o reagendamento ou a restituição dos valores antecipados, sem prejuízo de eventuais perdas e danos legalmente cabíveis.

4.3. Em caso de descumprimento injustificado por qualquer das partes, poderão ser exigidas as obrigações contratuais e as consequências previstas na legislação aplicável, observadas a proporcionalidade e as circunstâncias do caso concreto.

CLÁUSULA 5ª — DO PAGAMENTO DO CACHÊ

5.1. O(A) CONTRATANTE obriga-se a pagar o valor integral do cachê contratado, conforme a forma e o prazo estabelecidos na cláusula 1.2, desde que a apresentação seja realizada nos termos deste contrato.

5.2. O pagamento não poderá ser condicionado exclusivamente ao sucesso comercial do evento, à quantidade de público, ao consumo dos clientes ou ao faturamento do estabelecimento, salvo se as partes tiverem acordado expressamente uma modalidade de remuneração variável.

5.3. O cancelamento pelo(a) CONTRATANTE após o prazo de 24 (vinte e quatro) horas será regido pela multa estabelecida na cláusula 3.2, sem prejuízo de valores eventualmente devidos conforme a legislação e as condições específicas da contratação.

5.4. Em caso de atraso no pagamento, poderão incidir correção monetária, juros de mora e demais encargos legalmente cabíveis, observados os limites legais e o que tiver sido expressamente acordado entre as partes.

CLÁUSULA 6ª — DO DESCUMPRIMENTO E DAS MEDIDAS LEGAIS

6.1. O descumprimento injustificado das obrigações previstas neste contrato poderá ensejar notificação extrajudicial, tentativa de composição amigável, cobrança dos valores devidos e adoção das medidas judiciais cabíveis.

6.2. A parte prejudicada poderá buscar o cumprimento da obrigação, a cobrança da multa contratual, quando aplicável, e a reparação de perdas e danos comprovados, conforme os requisitos e limites previstos na legislação vigente.

6.3. As partes reconhecem que a adoção de medidas judiciais deverá observar o direito aplicável, não implicando garantia automática de condenação ou de recebimento de valores.

CLÁUSULA 7ª — DAS DISPOSIÇÕES GERAIS

7.1. Qualquer alteração das condições deste contrato deverá ser acordada entre as partes e registrada por escrito.

7.2. A tolerância de uma parte quanto ao descumprimento pontual de uma obrigação não implicará renúncia de direitos ou alteração automática das condições contratadas.

7.3. As comunicações referentes à contratação deverão ser encaminhadas aos contatos informados neste instrumento, cabendo às partes manter seus dados atualizados.

7.4. Este contrato obriga as partes e seus sucessores, nos limites permitidos pela legislação aplicável.

CLÁUSULA 8ª — DO FORO

8.1. As partes buscarão resolver amigavelmente eventuais controvérsias decorrentes deste contrato.

8.2. Não sendo possível a composição amigável, fica eleito o foro legalmente competente para solucionar eventuais conflitos, respeitadas as regras legais de competência e eventuais normas protetivas aplicáveis.

E, por estarem de acordo, as partes assinam o presente instrumento.

Local: {line}

Data: {d.EventDate}

CONTRATANTE / ESTABELECIMENTO

Nome: {V(d.ContractorName)}

CPF/CNPJ: {V(d.ContractorDocument)}

Assinatura: {line}

CONTRATADO(A) / ARTISTA

Nome: {V(d.ArtisticName)}

CPF/CNPJ: {V(d.ArtistDocument)}

Assinatura: {line}
""";
    }
}
