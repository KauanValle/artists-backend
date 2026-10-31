using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Persistence;
using ArtistPlatform.Domain.Common;
using ArtistPlatform.Domain.Entities;
using ArtistPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Application.Services;

public interface IBookingService
{
    Task<BookingDto> CreateAsync(CreateBookingRequestDto request);
    Task<BookingDto> CreateGuestAsync(CreateGuestBookingRequestDto request);
    Task<PagedResult<BookingDto>> ListAsync(BookingStatus? status, bool? isGuest, int page, int pageSize,
        string? busca = null, string? ordenarPor = null, string? direcao = null);
    Task<BookingDto> GetAsync(Guid id);
    Task<BookingDto> StartNegotiationAsync(Guid id);
    Task<BookingDto> AcceptAsync(Guid id);
    Task<BookingDto> RejectAsync(Guid id);
    Task<BookingDto> CancelAsync(Guid id);
    Task<CompleteShowDto> CompleteAsync(Guid id);
    Task<CompletionSummaryDto> GetCompletionByTokenAsync(string token);
    Task<CompletionSummaryDto> ConfirmCompletionByTokenAsync(string token);
}

public class BookingService(
    IAppDbContext db,
    ICurrentUserService currentUser,
    IUserDirectory userDirectory,
    INotificationService notifications) : IBookingService
{
    public async Task<BookingDto> CreateAsync(CreateBookingRequestDto request)
    {
        var contractorId = currentUser.EnsureContractor();

        if (request.EndTime <= request.StartTime)
            throw new ValidationException("O horário final deve ser maior que o inicial.");
        if (request.EventDate < DateOnly.FromDateTime(DateTime.Today))
            throw new ValidationException("A data do evento não pode estar no passado.");

        // RN009: não é possível solicitar o artista em dia/horário que ele bloqueou na agenda.
        await EnsureArtistNotBlockedAsync(request.ArtistId, request.EventDate, request.StartTime, request.EndTime);

        var artist = await db.Artists.AsNoTracking().FirstOrDefaultAsync(a => a.Id == request.ArtistId)
            ?? throw new NotFoundException("Artista não encontrado.");
        var artistUser = await db.Artists.AsNoTracking().Where(a => a.Id == request.ArtistId).Select(a => a.UserId).FirstAsync();

        // Adicionais (equipe/equipamento externos): valores calculados no servidor sobre a proposta.
        var addons = await ResolveAddonsAsync(request.ArtistId, request.Addons, request.Budget ?? 0);

        var booking = new BookingRequest
        {
            ArtistId = request.ArtistId,
            ContractorId = contractorId,
            EventDate = request.EventDate,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Location = request.Location,
            EventType = request.EventType,
            EstimatedAudience = request.EstimatedAudience,
            Budget = request.Budget.HasValue || addons.Count > 0 ? (request.Budget ?? 0) + addons.Sum(a => a.Amount) : null,
            Message = request.Message ?? string.Empty,
            Status = BookingStatus.Requested
        };
        db.BookingRequests.Add(booking);
        db.BookingAddons.AddRange(addons.Select(a => { a.BookingRequestId = booking.Id; return a; }));

        var contractorUserId = currentUser.UserId!.Value;
        db.Conversations.Add(new Conversation
        {
            BookingRequestId = booking.Id,
            ArtistUserId = artistUser,
            ContractorUserId = contractorUserId
        });

        await db.SaveChangesAsync();

        await notifications.NotifyAsync(
            artistUser,
            NotificationType.NewBookingRequest,
            "Nova solicitação de contratação",
            $"{artist.ArtisticName} recebeu uma nova solicitação para {request.EventDate:dd/MM/yyyy} às {request.StartTime}.",
            $"/shows/{booking.Id}");

        return await ToDtoAsync(booking, artist.ArtisticName, await GetContractorNameAsync(contractorId), addons);
    }

    /// <summary>
    /// Solicitação pela rota pública, sem cadastro: grava os dados de contato informados na hora
    /// (nome do responsável, estabelecimento e WhatsApp), sem contratante, conversa ou notificação
    /// para contratante — o artista entra em contato pelo WhatsApp informado.
    /// </summary>
    public async Task<BookingDto> CreateGuestAsync(CreateGuestBookingRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.GuestName))
            throw new ValidationException("Informe o nome do responsável.");
        if (string.IsNullOrWhiteSpace(request.GuestPhone))
            throw new ValidationException("Informe o WhatsApp do responsável.");
        if (request.Budget is not > 0)
            throw new ValidationException("Informe o valor da proposta.");
        if (request.EndTime <= request.StartTime)
            throw new ValidationException("O horário final deve ser maior que o inicial.");
        if (request.EventDate < DateOnly.FromDateTime(DateTime.Today))
            throw new ValidationException("A data do evento não pode estar no passado.");

        var artist = await db.Artists.AsNoTracking().FirstOrDefaultAsync(a => a.Id == request.ArtistId)
            ?? throw new NotFoundException("Artista não encontrado.");
        var isPublished = await db.ArtistProfiles.AsNoTracking()
            .AnyAsync(p => p.ArtistId == artist.Id && p.IsPublished);
        if (!isPublished)
            throw new NotFoundException("Artista não encontrado.");

        // RN009: não é possível solicitar o artista em dia/horário que ele bloqueou na agenda.
        await EnsureArtistNotBlockedAsync(request.ArtistId, request.EventDate, request.StartTime, request.EndTime);

        // Adicionais (equipe/equipamento externos): valores calculados no servidor sobre a proposta.
        var addons = await ResolveAddonsAsync(request.ArtistId, request.Addons, request.Budget);

        var booking = new BookingRequest
        {
            ArtistId = request.ArtistId,
            ContractorId = null,
            EventDate = request.EventDate,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Location = request.Location,
            EventType = request.EventType,
            EstimatedAudience = request.EstimatedAudience,
            Budget = request.Budget + addons.Sum(a => a.Amount),
            Message = request.Message ?? string.Empty,
            GuestName = request.GuestName.Trim(),
            GuestCompanyName = string.IsNullOrWhiteSpace(request.GuestCompanyName) ? null : request.GuestCompanyName.Trim(),
            GuestPhone = request.GuestPhone.Trim(),
            Status = BookingStatus.Requested
        };
        db.BookingRequests.Add(booking);
        db.BookingAddons.AddRange(addons.Select(a => { a.BookingRequestId = booking.Id; return a; }));
        await db.SaveChangesAsync();

        await notifications.NotifyAsync(
            artist.UserId,
            NotificationType.NewBookingRequest,
            "Nova solicitação de contratação",
            $"{request.GuestName.Trim()} ({request.GuestPhone.Trim()}) solicitou {artist.ArtisticName} para {request.EventDate:dd/MM/yyyy} às {request.StartTime}.",
            $"/shows/{booking.Id}");

        return await ToDtoAsync(booking, artist.ArtisticName, booking.GuestName, addons);
    }

    /// <summary>
    /// RN009: solicitação não pode cair em dia/horário bloqueado pelo artista (indisponível/pré-reservado)
    /// e, quando o artista já declarou dias de trabalho, só esses dias da semana podem ser solicitados.
    /// </summary>
    private async Task EnsureArtistNotBlockedAsync(Guid artistId, DateOnly eventDate, TimeOnly startTime, TimeOnly endTime)
    {
        var configuredWeekdays = await db.ArtistAvailabilities.AsNoTracking()
            .Where(a => a.ArtistId == artistId && a.Status == AvailabilityStatus.Available)
            .Select(a => (int)a.Date.DayOfWeek)
            .Distinct()
            .ToListAsync();

        if (configuredWeekdays.Count > 0 && !configuredWeekdays.Contains((int)eventDate.DayOfWeek))
            throw new BusinessRuleException("RN009: o artista não atende nesse dia da semana.");

        var hasBlock = await db.ArtistAvailabilities.AsNoTracking()
            .AnyAsync(a => a.ArtistId == artistId &&
                           a.Status != AvailabilityStatus.Available &&
                           a.Date == eventDate &&
                           a.StartTime < endTime &&
                           a.EndTime > startTime);
        if (hasBlock)
            throw new BusinessRuleException("RN009: o artista está indisponível nessa data e horário.");

        // RN017: evento agendado/confirmado na agenda do artista ocupa o horário —
        // incluindo o tempo de deslocamento/montagem configurado pelo artista.
        var travelMinutes = await db.Artists.AsNoTracking()
            .Where(a => a.Id == artistId)
            .Select(a => a.TravelTimeMinutes)
            .FirstOrDefaultAsync();
        var buffer = TimeSpan.FromMinutes(Math.Max(0, travelMinutes));
        var requestedStart = eventDate.ToDateTime(startTime, DateTimeKind.Utc) - buffer;
        var requestedEnd = eventDate.ToDateTime(endTime, DateTimeKind.Utc) + buffer;
        var hasEvent = await db.Events.AsNoTracking()
            .AnyAsync(e => e.ArtistId == artistId &&
                           (e.Status == EventStatus.Scheduled || e.Status == EventStatus.Confirmed) &&
                           e.StartDateTime < requestedEnd && e.EndDateTime > requestedStart);
        if (hasEvent)
            throw new BusinessRuleException("RN017: o artista já possui um evento agendado nesse horário.");
    }

    /// <summary>Lista com abas por status (PRD §10), filtrada pelo papel do usuário autenticado.</summary>
    /// <summary>
    /// Lista com filtro por status (abas do PRD §10), busca por nome/local,
    /// ordenação (data, valor, contratante, criação) e paginação — escopada ao papel.
    /// </summary>
    public async Task<PagedResult<BookingDto>> ListAsync(BookingStatus? status, bool? isGuest, int page, int pageSize,
        string? busca = null, string? ordenarPor = null, string? direcao = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var decrescente = !string.Equals(direcao, "asc", StringComparison.OrdinalIgnoreCase);

        var query = db.BookingRequests.AsNoTracking();

        if (currentUser.Role == UserRole.Admin)
        {
            if (status.HasValue)
                query = query.Where(b => b.Status == status.Value);
        }
        else if (currentUser.Role == UserRole.Artist)
            query = query.Where(b => b.ArtistId == currentUser.ArtistId);
        else if (currentUser.Role == UserRole.Contractor)
            query = query.Where(b => b.ContractorId == currentUser.ContractorId);
        else
            currentUser.EnsureAuthenticated();

        if (status.HasValue)
            query = query.Where(b => b.Status == status.Value);

        // Filtro para o painel admin: solicitações de visitante (sem cadastro).
        // IsGuest é computed property — na query usa a expressão mapeada (ContractorId nulo).
        if (isGuest.HasValue)
            query = query.Where(b => (b.ContractorId == null) == isGuest.Value);

        // Busca por nome (contratante/visitante/artista) ou local — case-insensitive
        // pelo padrão do projeto (ToLowerInvariant + Contains, traduzido para ILIKE).
        var termo = (busca ?? string.Empty).Trim().ToLowerInvariant();
        if (termo.Length > 0)
        {
            query = query.Where(b =>
                b.Location.ToLower().Contains(termo) ||
                b.GuestName.ToLower().Contains(termo) ||
                db.Contractors.Any(cn => cn.Id == b.ContractorId && cn.Name.ToLower().Contains(termo)) ||
                db.Artists.Any(a => a.Id == b.ArtistId && a.ArtisticName.ToLower().Contains(termo)));
        }

        var total = await query.CountAsync();

        // Ordenação: data do show, valor, nome do contratante/visitante ou criação (padrão).
        var campo = (ordenarPor ?? string.Empty).Trim().ToLowerInvariant();
        IOrderedQueryable<BookingRequest> ordenada = campo switch
        {
            "data" => decrescente
                ? query.OrderByDescending(b => b.EventDate).ThenByDescending(b => b.StartTime)
                : query.OrderBy(b => b.EventDate).ThenBy(b => b.StartTime),
            "valor" => decrescente
                ? query.OrderByDescending(b => b.Budget ?? 0m)
                : query.OrderBy(b => b.Budget ?? 0m),
            "contratante" => decrescente
                ? query.OrderByDescending(b =>
                    b.ContractorId == null
                        ? b.GuestName
                        : db.Contractors.Where(cn => cn.Id == b.ContractorId).Select(cn => cn.Name).FirstOrDefault())
                : query.OrderBy(b =>
                    b.ContractorId == null
                        ? b.GuestName
                        : db.Contractors.Where(cn => cn.Id == b.ContractorId).Select(cn => cn.Name).FirstOrDefault()),
            _ => decrescente
                ? query.OrderByDescending(b => b.CreatedAt)
                : query.OrderBy(b => b.CreatedAt),
        };

        var bookings = await ordenada
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<BookingDto>(await MapAllAsync(bookings), page, pageSize, total);
    }

    public async Task<BookingDto> GetAsync(Guid id)
    {
        var booking = await EnsureAccessAsync(id);
        return (await MapAllAsync([booking])).First();
    }

    /// <summary>Artista inicia a negociação (REQUESTED → NEGOTIATING).</summary>
    public async Task<BookingDto> StartNegotiationAsync(Guid id)
    {
        var booking = await EnsureArtistOwnerAsync(id);
        if (booking.Status != BookingStatus.Requested)
            throw new BusinessRuleException("Só é possível iniciar negociação a partir de uma solicitação nova.");

        booking.Status = BookingStatus.Negotiating;
        booking.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return (await MapAllAsync([booking])).First();
    }

    /// <summary>Artista recusa a solicitação (RN018: recusada não volta a ser aceita).</summary>
    public async Task<BookingDto> RejectAsync(Guid id)
    {
        var booking = await EnsureArtistOwnerAsync(id);
        if (!booking.CanEvolve)
            throw new BusinessRuleException("RN018: esta solicitação não pode mais ser recusada.");

        var artist = await db.Artists.AsNoTracking().FirstAsync(a => a.Id == booking.ArtistId);
        var contractorUser = booking.ContractorId is null
            ? null
            : (Guid?)await db.Contractors.AsNoTracking().Where(c => c.Id == booking.ContractorId).Select(c => c.UserId).FirstAsync();

        booking.Status = BookingStatus.Rejected;
        booking.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        if (contractorUser.HasValue)
            await notifications.NotifyAsync(contractorUser.Value, NotificationType.ProposalRejected,
                "Solicitação recusada",
                $"{artist.ArtisticName} recusou a solicitação para {booking.EventDate:dd/MM/yyyy}.",
                $"/solicitacoes/{booking.Id}");

        return (await MapAllAsync([booking])).First();
    }

    public async Task<BookingDto> CancelAsync(Guid id)
    {
        var booking = await EnsureAccessAsync(id);
        var actorIsArtist = currentUser.Role == UserRole.Artist;

        if (booking.Status == BookingStatus.Completed)
            throw new BusinessRuleException("Shows realizados não podem ser cancelados.");
        if (booking.Status is not (BookingStatus.Requested or BookingStatus.Negotiating or BookingStatus.Proposed or BookingStatus.Confirmed))
            throw new BusinessRuleException("Esta solicitação não pode mais ser cancelada.");

        // Show confirmado: cancela também o evento na agenda para liberar a data.
        if (booking.Status == BookingStatus.Confirmed && booking.EventId is not null)
        {
            var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == booking.EventId);
            if (ev is not null && ev.BlocksAgenda)
            {
                ev.Status = EventStatus.Cancelled;
                ev.UpdatedAt = DateTime.UtcNow;
            }
        }

        booking.Status = BookingStatus.Cancelled;
        booking.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Notifica as duas partes quando identificáveis (contratante visitante não tem conta).
        var message = $"A contratação de {booking.EventDate:dd/MM/yyyy} às {booking.StartTime} foi cancelada.";
        var artistUserId = await db.Artists.AsNoTracking()
            .Where(a => a.Id == booking.ArtistId)
            .Select(a => a.UserId)
            .FirstOrDefaultAsync();
        if (artistUserId != Guid.Empty)
            await notifications.NotifyAsync(artistUserId, NotificationType.BookingCancelled, "Contratação cancelada", message, $"/shows/{booking.Id}");

        if (booking.ContractorId is not null)
        {
            var contractorUserId = await db.Contractors.AsNoTracking()
                .Where(c => c.Id == booking.ContractorId)
                .Select(c => c.UserId)
                .FirstOrDefaultAsync();
            if (contractorUserId != Guid.Empty)
                await notifications.NotifyAsync(contractorUserId, NotificationType.BookingCancelled, "Contratação cancelada", message, $"/contractor/solicitacoes/{booking.Id}");
        }

        return (await MapAllAsync([booking])).First();
    }

    /// <summary>
    /// Conclusão do show em dupla confirmação: artista e contratante precisam confirmar.
    /// Enquanto falta uma parte, o evento guarda o estado e o token que gera o link
    /// público de confirmação do contratante.
    /// </summary>
    public async Task<CompleteShowDto> CompleteAsync(Guid id)
    {
        var booking = await EnsureAccessAsync(id);

        if (booking.Status == BookingStatus.Completed)
            return await BuildCompleteDtoAsync(booking);
        if (booking.Status != BookingStatus.Confirmed || booking.EventId is null)
            throw new BusinessRuleException("Apenas shows confirmados podem ser concluídos.");

        var ev = await db.Events.FirstAsync(e => e.Id == booking.EventId);
        var agora = DateTime.UtcNow;

        if (currentUser.Role == UserRole.Contractor)
            ev.ContractorCompletedAt ??= agora;
        else
            ev.ArtistCompletedAt ??= agora;

        await FinalizarConclusaoAsync(booking, ev);

        // Avisa a parte que ainda falta confirmar.
        if (booking.Status != BookingStatus.Completed)
        {
            ev.CompletionToken ??= Guid.NewGuid().ToString("N");
            if (ev.ContractorCompletedAt is null)
            {
                var artistName = await db.Artists.AsNoTracking()
                    .Where(a => a.Id == booking.ArtistId).Select(a => a.ArtisticName).FirstAsync();
                await NotificarContratanteAsync(booking, "Confirme a conclusão do show",
                    $"{artistName} indicou que o show de {booking.EventDate:dd/MM/yyyy} aconteceu — confirme a conclusão.", $"/contractor/solicitacoes/{booking.Id}");
            }
            else
                await NotificarArtistaAsync(booking, "O contratante confirmou a conclusão",
                    $"Confirme a conclusão do show de {booking.EventDate:dd/MM/yyyy} para encerrar a contratação.", $"/shows/{booking.Id}");
        }

        await db.SaveChangesAsync();
        return await BuildCompleteDtoAsync(booking);
    }

    /// <summary>Resumo do show pelo token do link de confirmação (contratante logado).</summary>
    public async Task<CompletionSummaryDto> GetCompletionByTokenAsync(string token)
    {
        var (ev, booking, artistName) = await CarregarPorTokenAsync(token);
        EnsureContratanteDoBooking(booking);
        return new CompletionSummaryDto(token, artistName, booking.EventType, booking.EventDate,
            booking.StartTime, booking.EndTime, booking.Location,
            ev.ArtistCompletedAt is not null, ev.ContractorCompletedAt is not null, booking.Status);
    }

    /// <summary>Confirmação do contratante pelo link (exige login do contratante do show).</summary>
    public async Task<CompletionSummaryDto> ConfirmCompletionByTokenAsync(string token)
    {
        var (ev, booking, artistName) = await CarregarPorTokenAsync(token);
        EnsureContratanteDoBooking(booking);

        if (booking.Status == BookingStatus.Completed)
            return await ResumoAsync(ev, booking, artistName);

        ev.ContractorCompletedAt ??= DateTime.UtcNow;
        await FinalizarConclusaoAsync(booking, ev);

        if (booking.Status != BookingStatus.Completed)
            await NotificarArtistaAsync(booking, "O contratante confirmou a conclusão",
                $"Confirme a conclusão do show de {booking.EventDate:dd/MM/yyyy} para encerrar a contratação.", $"/shows/{booking.Id}");

        await db.SaveChangesAsync();
        return await ResumoAsync(ev, booking, artistName);
    }

    /// <summary>O link de conclusão só vale para o contratante da própria contratação.</summary>
    private void EnsureContratanteDoBooking(BookingRequest booking)
    {
        if (currentUser.Role == UserRole.Admin) return;
        if (currentUser.Role != UserRole.Contractor || booking.ContractorId != currentUser.ContractorId)
            throw new ForbiddenException("Apenas o contratante deste show pode confirmar a conclusão.");
    }

    private async Task<(Event Ev, BookingRequest Booking, string ArtistName)> CarregarPorTokenAsync(string token)
    {
        // Rastreado: a confirmação pública precisa persistir a data no evento.
        var ev = await db.Events.FirstOrDefaultAsync(e => e.CompletionToken == token)
            ?? throw new NotFoundException("Link de confirmação inválido ou expirado.");
        var booking = await db.BookingRequests.FirstAsync(b => b.EventId == ev.Id);
        var artistName = await db.Artists.AsNoTracking()
            .Where(a => a.Id == booking.ArtistId).Select(a => a.ArtisticName).FirstAsync();
        return (ev, booking, artistName);
    }

    /// <summary>Quando as duas partes confirmaram, encerra: booking e evento ficam concluídos.</summary>
    private async Task FinalizarConclusaoAsync(BookingRequest booking, Event ev)
    {
        if (ev.ArtistCompletedAt is null || ev.ContractorCompletedAt is null)
            return;

        ev.Status = EventStatus.Completed;
        ev.UpdatedAt = DateTime.UtcNow;
        booking.Status = BookingStatus.Completed;
        booking.UpdatedAt = DateTime.UtcNow;

        var mensagem = $"O show de {booking.EventDate:dd/MM/yyyy} foi concluído pelas duas partes. Bom trabalho!";
        await NotificarArtistaAsync(booking, "Show concluído", mensagem, $"/shows/{booking.Id}");
        await NotificarContratanteAsync(booking, "Show concluído", mensagem, $"/contractor/solicitacoes/{booking.Id}");
    }

    private async Task<CompleteShowDto> BuildCompleteDtoAsync(BookingRequest booking)
    {
        string? token = null;
        bool artistConfirmed = false, contractorConfirmed = false;
        if (booking.EventId is not null)
        {
            var ev = await db.Events.AsNoTracking().FirstAsync(e => e.Id == booking.EventId);
            artistConfirmed = ev.ArtistCompletedAt is not null;
            contractorConfirmed = ev.ContractorCompletedAt is not null;
            if (!artistConfirmed || !contractorConfirmed)
                token = ev.CompletionToken;
        }
        return new CompleteShowDto(booking.Id, booking.Status, artistConfirmed, contractorConfirmed, token);
    }

    private async Task<CompletionSummaryDto> ResumoAsync(Event ev, BookingRequest booking, string artistName) =>
        new(ev.CompletionToken ?? string.Empty, artistName, booking.EventType, booking.EventDate,
            booking.StartTime, booking.EndTime, booking.Location,
            ev.ArtistCompletedAt is not null, ev.ContractorCompletedAt is not null, booking.Status);

    private async Task NotificarArtistaAsync(BookingRequest booking, string titulo, string mensagem, string link)
    {
        var artistUserId = await db.Artists.AsNoTracking()
            .Where(a => a.Id == booking.ArtistId).Select(a => a.UserId).FirstOrDefaultAsync();
        if (artistUserId != Guid.Empty)
            await notifications.NotifyAsync(artistUserId, NotificationType.ShowCompletionRequested, titulo, mensagem, link);
    }

    private async Task NotificarContratanteAsync(BookingRequest booking, string titulo, string mensagem, string link)
    {
        if (booking.ContractorId is null) return;
        var contractorUserId = await db.Contractors.AsNoTracking()
            .Where(c => c.Id == booking.ContractorId).Select(c => c.UserId).FirstOrDefaultAsync();
        if (contractorUserId != Guid.Empty)
            await notifications.NotifyAsync(contractorUserId, NotificationType.ShowCompletionRequested, titulo, mensagem, link);
    }

    /// <summary>
    /// Resolve os adicionais da contratação (equipe externa / equipamento externo do artista):
    /// valida disponibilidade, calcula o valor (percentual sobre a proposta base ou fixo) e
    /// devolve as linhas a persistir. A soma é acrescida ao Budget da solicitação.
    /// </summary>
    private async Task<List<BookingAddon>> ResolveAddonsAsync(Guid artistId, List<BookingAddonItemRequest>? addons, decimal baseBudget)
    {
        var result = new List<BookingAddon>();
        if (addons is null || addons.Count == 0) return result;
        if (addons.Count > 30)
            throw new ValidationException("Muitos adicionais selecionados.");

        foreach (var group in addons.GroupBy(a => new { a.ItemType, a.ItemId }).Where(g => g.Count() > 1))
            throw new ValidationException("Há adicionais repetidos na seleção.");

        var teamIds = addons.Where(a => a.ItemType == AddonItemType.TeamMember).Select(a => a.ItemId).ToList();
        var equipmentIds = addons.Where(a => a.ItemType == AddonItemType.Equipment).Select(a => a.ItemId).ToList();

        var team = teamIds.Count > 0
            ? await db.TeamMembers.AsNoTracking()
                .Where(m => teamIds.Contains(m.Id) && m.ArtistId == artistId && m.AllocationType == AllocationType.Externo)
                .ToDictionaryAsync(m => m.Id)
            : [];
        var equipment = equipmentIds.Count > 0
            ? await db.Equipment.AsNoTracking()
                .Where(e => equipmentIds.Contains(e.Id) && e.ArtistId == artistId && e.AllocationType == AllocationType.Externo)
                .ToDictionaryAsync(e => e.Id)
            : [];

        foreach (var addon in addons)
        {
            if (addon.ItemType == AddonItemType.TeamMember)
            {
                var member = team.GetValueOrDefault(addon.ItemId)
                    ?? throw new ValidationException("Um dos adicionais de equipe não está mais disponível para este artista.");
                var amount = member.DefaultShareType == ShareType.Percentage
                    ? decimal.Round(baseBudget * member.DefaultShareValue / 100m, 2)
                    : member.DefaultShareValue;
                result.Add(new BookingAddon
                {
                    ItemType = addon.ItemType,
                    ItemId = addon.ItemId,
                    Name = member.Name,
                    ShareType = member.DefaultShareType,
                    ShareValue = member.DefaultShareValue,
                    Amount = amount
                });
            }
            else if (addon.ItemType == AddonItemType.Equipment)
            {
                var eq = equipment.GetValueOrDefault(addon.ItemId)
                    ?? throw new ValidationException("Um dos adicionais de equipamento não está mais disponível para este artista.");
                var amount = eq.ShareType == ShareType.Percentage
                    ? decimal.Round(baseBudget * eq.ShareValue / 100m, 2)
                    : eq.ShareValue;
                result.Add(new BookingAddon
                {
                    ItemType = addon.ItemType,
                    ItemId = addon.ItemId,
                    Name = eq.Name,
                    ShareType = eq.ShareType,
                    ShareValue = eq.ShareValue,
                    Amount = amount
                });
            }
            else
            {
                throw new ValidationException("Tipo de adicional inválido.");
            }
        }

        return result;
    }

    /// <summary>
    /// Aceite direto pelo artista, sem proposta formal: usa o orçamento informado pelo contratante
    /// como valor final e dispara os mesmos efeitos do aceite de proposta (PRD §11 — RN007/RN008/RN010).
    /// </summary>
    public async Task<BookingDto> AcceptAsync(Guid id)
    {
        var booking = await EnsureArtistOwnerAsync(id);

        if (booking.Status is not (BookingStatus.Requested or BookingStatus.Negotiating))
            throw new BusinessRuleException("O aceite direto está disponível apenas para solicitações novas ou em negociação.");

        var hasPendingProposal = await db.Proposals.AsNoTracking()
            .AnyAsync(p => p.BookingRequestId == booking.Id && p.Status == ProposalStatus.Pending);
        if (hasPendingProposal)
            throw new BusinessRuleException("Já existe uma proposta pendente para esta solicitação. Aceite ou recuse a proposta.");

        if (booking.Budget is not > 0)
            throw new BusinessRuleException("Esta solicitação não tem orçamento informado. Envie uma proposta para definir o valor.");

        var interval = booking.ToInterval();

        // RN017/RN009: a agenda não pode conflitar
        var hasEventConflict = await db.Events.AsNoTracking()
            .AnyAsync(e => e.ArtistId == booking.ArtistId &&
                           (e.Status == EventStatus.Scheduled || e.Status == EventStatus.Confirmed) &&
                           e.StartDateTime < interval.End && e.EndDateTime > interval.Start);
        if (hasEventConflict)
            throw new BusinessRuleException("RN017: você já possui um evento confirmado nesse horário.");

        var blockedRows = await db.ArtistAvailabilities.AsNoTracking()
            .Where(a => a.ArtistId == booking.ArtistId && a.Status != AvailabilityStatus.Available &&
                        a.Date == booking.EventDate)
            .ToListAsync();
        foreach (var row in blockedRows)
        {
            var blockedInterval = new Interval(
                row.Date.ToDateTime(row.StartTime, DateTimeKind.Utc),
                row.Date.ToDateTime(row.IsAllDay ? new TimeOnly(23, 59, 59) : row.EndTime, DateTimeKind.Utc));
            if (blockedInterval.Overlaps(interval))
                throw new BusinessRuleException("RN017: o horário está bloqueado na sua agenda.");
        }

        var artist = await db.Artists.AsNoTracking().FirstAsync(a => a.Id == booking.ArtistId);
        var contractorUser = booking.ContractorId is null
            ? null
            : (Guid?)await db.Contractors.AsNoTracking()
                .Where(c => c.Id == booking.ContractorId).Select(c => c.UserId).FirstAsync();

        // RN007: aceite cria/confirma o evento (RN008: bloqueia a agenda).
        // O tempo de deslocamento/montagem do artista expande a reserva antes e depois do show.
        var buffer = TimeSpan.FromMinutes(artist.TravelTimeMinutes);
        // Visitante: evento pré-reservado (soft) — formaliza quando o contratante criar conta.
        var eventStatus = booking.IsGuest ? EventStatus.PreReserved : EventStatus.Confirmed;
        var ev = new Event
        {
            ArtistId = booking.ArtistId,
            ContractorId = booking.ContractorId,
            BookingRequestId = booking.Id,
            Title = $"{artist.ArtisticName} — {booking.EventType}",
            Type = booking.EventType,
            StartDateTime = interval.Start - buffer,
            EndDateTime = interval.End + buffer,
            Location = booking.Location,
            Description = booking.Message,
            Status = eventStatus
        };
        db.Events.Add(ev);

        booking.EventId = ev.Id;
        // Visitante: fica "Aguardando conta" até o contratante criar o cadastro e formalizar.
        booking.Status = booking.IsGuest ? BookingStatus.AwaitingAccount : BookingStatus.Confirmed;
        booking.UpdatedAt = DateTime.UtcNow;

        // RN010: receita PENDING com o orçamento acordado
        db.FinancialTransactions.Add(new FinancialTransaction
        {
            ArtistId = booking.ArtistId,
            Type = TransactionType.Income,
            Category = "Show",
            Amount = booking.Budget.Value,
            DueDate = booking.EventDate,
            Status = TransactionStatus.Pending,
            EventId = ev.Id,
            Notes = $"Receita gerada pelo aceite direto da solicitação (contratação {booking.Id})."
        });

        await db.SaveChangesAsync();

        await notifications.NotifyAsync(artist.UserId, NotificationType.ShowConfirmed,
            "Show confirmado",
            $"Solicitação aceita! Show confirmado para {booking.EventDate:dd/MM/yyyy} às {booking.StartTime} em {booking.Location}.",
            $"/shows/{booking.Id}");
        if (contractorUser.HasValue)
            await notifications.NotifyAsync(contractorUser.Value, NotificationType.ShowConfirmed,
                "Solicitação aceita",
                $"{artist.ArtisticName} aceitou sua solicitação de {booking.Budget.Value:C}. O evento está confirmado.",
                $"/solicitacoes/{booking.Id}");

        return (await MapAllAsync([booking])).First();
    }

    private async Task<BookingRequest> EnsureArtistOwnerAsync(Guid id)
    {
        var booking = await db.BookingRequests.FirstOrDefaultAsync(b => b.Id == id)
            ?? throw new NotFoundException("Solicitação não encontrada.");
        if (booking.ArtistId != currentUser.ArtistId && currentUser.Role != UserRole.Admin)
            throw new ForbiddenException("Apenas o artista responsável pode executar esta ação.");
        return booking;
    }

    private async Task<BookingRequest> EnsureAccessAsync(Guid id)
    {
        var booking = await db.BookingRequests.FirstOrDefaultAsync(b => b.Id == id)
            ?? throw new NotFoundException("Solicitação não encontrada.");

        var allowed =
            (currentUser.Role == UserRole.Artist && booking.ArtistId == currentUser.ArtistId) ||
            (currentUser.Role == UserRole.Contractor && booking.ContractorId == currentUser.ContractorId) ||
            currentUser.Role == UserRole.Admin;
        if (!allowed)
            throw new ForbiddenException();
        return booking;
    }

    private async Task<List<BookingDto>> MapAllAsync(List<BookingRequest> bookings)
    {
        if (bookings.Count == 0) return [];

        var artistIds = bookings.Select(b => b.ArtistId).Distinct().ToList();
        var contractorIds = bookings.Select(b => b.ContractorId).Distinct().ToList();
        var bookingIds = bookings.Select(b => b.Id).ToList();

        var artists = await db.Artists.AsNoTracking()
            .Where(a => artistIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => new { a.ArtisticName, a.ProfilePhotoUrl });
        var registeredContractorIds = contractorIds.Where(id => id.HasValue).Select(id => id!.Value).ToList();
        var contractors = await db.Contractors.AsNoTracking()
            .Where(c => registeredContractorIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name);
        var conversations = await db.Conversations.AsNoTracking()
            .Where(c => bookingIds.Contains(c.BookingRequestId))
            .ToDictionaryAsync(c => c.BookingRequestId, c => c.Id);

        var addons = await db.BookingAddons.AsNoTracking()
            .Where(a => bookingIds.Contains(a.BookingRequestId))
            .ToListAsync();
        var addonsByBooking = addons
            .GroupBy(a => a.BookingRequestId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(a => new BookingAddonDto(a.Id, a.ItemType, a.ItemId, a.Name, a.ShareType, a.ShareValue, a.Amount)).ToList());

        return bookings
            .Select(b =>
            {
                var bookingAddons = addonsByBooking.TryGetValue(b.Id, out var bookingAddonList)
                    ? bookingAddonList
                    : new List<BookingAddonDto>();
                return new BookingDto(
            b.Id,
            b.ArtistId,
            artists.GetValueOrDefault(b.ArtistId)?.ArtisticName ?? "Artista",
            artists.GetValueOrDefault(b.ArtistId)?.ProfilePhotoUrl,
            b.ContractorId,
            b.IsGuest ? b.GuestName : contractors.GetValueOrDefault(b.ContractorId!.Value, "Contratante"),
            b.IsGuest,
            b.GuestCompanyName,
            b.IsGuest ? b.GuestPhone : null,
            b.EventDate,
            b.StartTime,
            b.EndTime,
            b.Location,
            b.EventType,
            b.EstimatedAudience,
            b.Budget,
            b.Message,
            b.Status,
            b.EventId,
            b.AcceptedProposalId,
            conversations.GetValueOrDefault(b.Id, Guid.Empty),
            b.CreatedAt,
            Addons: bookingAddons);
        }).ToList();
    }

    private async Task<string> GetContractorNameAsync(Guid contractorId)
    {
        return await db.Contractors.AsNoTracking()
            .Where(c => c.Id == contractorId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync() ?? "Contratante";
    }

    private async Task<BookingDto> ToDtoAsync(
        BookingRequest booking,
        string artistName,
        string contractorName,
        List<BookingAddon>? addons = null)
    {
        var conversation = await db.Conversations.AsNoTracking()
            .FirstOrDefaultAsync(c => c.BookingRequestId == booking.Id);

        var addonDtos = (addons ?? [])
            .Select(a => new BookingAddonDto(a.Id, a.ItemType, a.ItemId, a.Name, a.ShareType, a.ShareValue, a.Amount))
            .ToList();

        return new BookingDto(
            booking.Id, booking.ArtistId, artistName, null, booking.ContractorId, contractorName,
            booking.IsGuest, booking.GuestCompanyName, booking.IsGuest ? booking.GuestPhone : null,
            booking.EventDate, booking.StartTime, booking.EndTime, booking.Location, booking.EventType,
            booking.EstimatedAudience, booking.Budget, booking.Message, booking.Status,
            booking.EventId, booking.AcceptedProposalId, conversation?.Id ?? Guid.Empty, booking.CreatedAt,
            Addons: addonDtos);
    }
}
