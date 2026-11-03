# -*- coding: utf-8 -*-
import io

# 1) BookingService.AcceptAsync — não-guest vai para AwaitingPayment (sem evento/receita)
path = 'src/ArtistPlatform.Application/Services/BookingService.cs'
with io.open(path, encoding='utf-8') as f:
    c = f.read()

old = """        // RN007: aceite cria/confirma o evento (RN008: bloqueia a agenda).
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
    }"""
new = """        // Pagamento obrigatório (InfinityPay): para contratantes cadastrados, o aceite não
        // confirma o show — fica AGUARDANDO_PAGAMENTO. Evento, agenda e receita são criados
        // na confirmação do webhook do pagamento. Visitantes seguem o fluxo de conta.
        if (!booking.IsGuest)
        {
            booking.Status = BookingStatus.AwaitingPayment;
            booking.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            if (contractorUser.HasValue)
                await notifications.NotifyAsync(
                    contractorUser.Value,
                    NotificationType.ShowPaymentPending,
                    "Realize o pagamento do show",
                    $"{artist.ArtisticName} aceitou sua solicitação de {booking.Budget.Value:C}. Conclua o pagamento para confirmar o show de {booking.EventDate:dd/MM/yyyy}.",
                    $"/solicitacoes/{booking.Id}");

            return (await MapAllAsync([booking])).First();
        }

        // Visitante: evento pré-reservado (soft) — formaliza quando o contratante criar conta.
        var buffer = TimeSpan.FromMinutes(artist.TravelTimeMinutes);
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
            Status = EventStatus.PreReserved
        };
        db.Events.Add(ev);

        booking.EventId = ev.Id;
        booking.Status = BookingStatus.AwaitingAccount;
        booking.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        await notifications.NotifyAsync(artist.UserId, NotificationType.ShowConfirmed,
            "Solicitação aceita",
            $"Solicitação aceita! Show de {booking.EventDate:dd/MM/yyyy} às {booking.StartTime} em {booking.Location} pré-reservado até a formalização do contratante.",
            $"/shows/{booking.Id}");

        return (await MapAllAsync([booking])).First();
    }"""
assert c.count(old) == 1, 'AcceptAsync'
c = c.replace(old, new)

# CancelAsync: AwaitingPayment também pode ser cancelada
old = """        if (booking.Status is not (BookingStatus.Requested or BookingStatus.Negotiating or BookingStatus.Proposed or BookingStatus.Confirmed))
            throw new BusinessRuleException("Esta solicitação não pode mais ser cancelada.");"""
new = """        if (booking.Status is not (BookingStatus.Requested or BookingStatus.Negotiating or BookingStatus.Proposed
                or BookingStatus.Confirmed or BookingStatus.AwaitingPayment))
            throw new BusinessRuleException("Esta solicitação não pode mais ser cancelada.");"""
assert c.count(old) == 1, 'CancelAsync'
c = c.replace(old, new)

with io.open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(c)
print('BookingService ok')

# 2) ProposalService.AcceptAsync — mesma regra
path = 'src/ArtistPlatform.Application/Services/ProposalService.cs'
with io.open(path, encoding='utf-8') as f:
    c = f.read()

old = """        // 2) evento criado/confirmado (RN007) — bloqueia a agenda (RN008).
        // O tempo de deslocamento/montagem do artista expande a reserva antes e depois do show.
        var buffer = TimeSpan.FromMinutes(artist.TravelTimeMinutes);
        var ev = new Event
        {
            ArtistId = proposal.ArtistId,
            ContractorId = proposal.ContractorId,
            BookingRequestId = booking.Id,
            Title = $"{artist.ArtisticName} — {booking.EventType}",
            Type = booking.EventType,
            StartDateTime = interval.Start - buffer,
            EndDateTime = interval.End + buffer,
            Location = booking.Location,
            Description = booking.Message,
            Status = EventStatus.Confirmed
        };
        db.Events.Add(ev);

        // 3) solicitação ACCEPTED → CONFIRMED
        booking.AcceptedProposalId = proposal.Id;
        booking.EventId = ev.Id;
        booking.Status = BookingStatus.Confirmed;
        booking.UpdatedAt = clock.UtcNow;

        // 4) receita PENDING (RN010)
        var income = new FinancialTransaction
        {
            ArtistId = proposal.ArtistId,
            Type = TransactionType.Income,
            Category = "Show",
            Amount = proposal.FinalAmount,
            DueDate = booking.EventDate,
            Status = TransactionStatus.Pending,
            EventId = ev.Id,
            Notes = $"Receita gerada pelo aceite da proposta (contratação {booking.Id})."
        };
        db.FinancialTransactions.Add(income);

        await db.SaveChangesAsync();"""
new = """        // 2) Pagamento obrigatório (InfinityPay): o aceite NÃO confirma o show — fica
        // AGUARDANDO_PAGAMENTO. Evento, agenda e receita nascem na confirmação do webhook.
        booking.AcceptedProposalId = proposal.Id;
        booking.Status = BookingStatus.AwaitingPayment;
        booking.UpdatedAt = clock.UtcNow;

        await db.SaveChangesAsync();"""
assert c.count(old) == 1, 'ProposalAccept confirmacao'
c = c.replace(old, new)
with io.open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(c)
print('ProposalService ok')
PYEOF
