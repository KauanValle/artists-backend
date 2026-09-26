# API Contract — Artist Platform

ASP.NET Core Web API (.NET 10). Base URL local: `http://localhost:5090`.
Swagger UI em `/swagger` (Development).

## Convenções

- **Auth**: `Authorization: Bearer <JWT>` (access token 120 min + refresh token 7 dias com rotação).
- **Enums**: serializados como string (`"Requested"`, `"Income"`, ...).
- **Datas**: `yyyy-MM-dd` (DateOnly), `HH:mm:ss` (TimeOnly), ISO-8601 UTC (DateTime).
- **Erros** (middleware): `{ "error": "mensagem", "title": "...", "status": <code> }`

| Status | Quando |
|---|---|
| 400 | Dados inválidos (`ValidationException`) |
| 401 | Não autenticado / credenciais inválidas |
| 403 | Sem permissão sobre o recurso (`ForbiddenException`) |
| 404 | Recurso inexistente |
| 422 | Regra de negócio RN001–RN020 (`BusinessRuleException`) |

## /api/auth

| Método | Rota | Auth | Corpo → Resposta |
|---|---|---|---|
| POST | `/auth/register/artist` | — | `{ email, password, name, artisticName, artistType, city, state, phone, categoryId }` → `AuthResponse` |
| POST | `/auth/register/contractor` | — | `{ email, password, name, contractorType, phone }` → `AuthResponse` |
| POST | `/auth/login` | — | `{ email, password }` → `AuthResponse` |
| POST | `/auth/refresh` | — | `{ refreshToken }` → `AuthResponse` (rotação) |
| POST | `/auth/logout` | ✔ | `{ refreshToken }` → 204 |
| POST | `/auth/forgot-password` | — | `{ email }` → `{ token }` (token só em Development) |
| POST | `/auth/reset-password` | — | `{ email, token, newPassword }` → 204 |
| GET | `/auth/me` | ✔ | → `User` |

`AuthResponse = { accessToken, refreshToken, expiresAtUtc, user }`
`User = { id, email, displayName, role, artistId?, contractorId? }`

## /api/artists · /api/categories

| Método | Rota | Auth |
|---|---|---|
| GET | `/artists/search?search&categoryId&city&date&startTime&durationMinutes&minPrice&maxPrice&minRating&page&pageSize` | — (RN020: valida intervalo exato) |
| GET | `/artists/{id}` · `/artists/slug/{slug}` | — (perfil público) |
| GET | `/artists/me` | Artista |
| PUT | `/artists/me` — onboarding (PRD §6) | Artista |
| PUT | `/artists/me/publish` — `{ publish: bool }` | Artista |
| GET | `/categories` | — |

## Disponibilidade e Cachês

| Método | Rota | Auth |
|---|---|---|
| GET | `/artists/{artistId}/availability?from&to` | — |
| POST / PUT `{id}` / DELETE `{id}` | `/artists/me/availability` — `{ date, startTime, endTime, isAllDay, status, note? }` | Artista (RN009) |
| GET | `/artists/{artistId}/fees` | — |
| GET / POST / PUT `{id}` / DELETE `{id}` | `/artists/me/fees` — `{ durationMinutes, price, description? }` | Artista (RN004/RN005) |

## /api/bookings

| Método | Rota | Auth | Observação |
|---|---|---|---|
| POST | `/bookings` — `{ artistId, eventDate, startTime, endTime, location, eventType, estimatedAudience?, budget?, message }` | Contratante | cria REQUESTED + conversa + notificação |
| GET | `/bookings?status&page&pageSize` | ✔ | escopado ao papel (abas PRD §10) |
| GET | `/bookings/{id}` | ✔ | artista dono / contratante dono / admin |
| POST | `/bookings/{id}/negotiate` | Artista | REQUESTED → NEGOTIATING |
| POST | `/bookings/{id}/accept` | Artista | **Aceite direto sem proposta**: usa o orçamento da solicitação como valor final; exige orçamento > 0, sem proposta pendente e agenda livre (RN007/RN008/RN010/RN017) → CONFIRMED |
| POST | `/bookings/{id}/reject` | Artista | → REJECTED (RN018: terminal) |
| POST | `/bookings/{id}/cancel` | ✔ | antes da confirmação |

## /api/proposals

| Método | Rota | Auth | Observação |
|---|---|---|---|
| POST | `/proposals` — `{ bookingRequestId, items[{description, amount}], travelCost, equipmentCost, discount, validityDays, notes }` | Artista | valor final = itens + deslocamento + equipamento − desconto |
| GET | `/proposals/{id}` · `/proposals/booking/{bookingId}` | ✔ | |
| POST | `/proposals/{id}/accept` | Contratante | PRD §11: proposta ACCEPTED, evento CONFIRMED criado, agenda bloqueada, receita PENDING, notificações (RN007/RN008/RN010/RN017/RN019) |
| POST | `/proposals/{id}/reject` | Contratante | negociação continua aberta |

## /api/conversations

| Método | Rota | Auth |
|---|---|---|
| GET | `/conversations` | ✔ |
| GET | `/conversations/{id}` — retorna `{ conversation, messages }` e marca leitura | ✔ (participante) |
| POST | `/conversations/{id}/messages` — `{ content }` | ✔ |
| PUT | `/conversations/{id}/read` | ✔ |

## /api/events (agenda do artista)

| Método | Rota | Observação |
|---|---|---|
| GET | `/events?from&to&type&status` | mês/semana/dia via from/to |
| POST | `/events` — `{ title, type, startDateTime, endDateTime, location, description }` | valida conflito RN009 |
| GET / PUT / DELETE | `/events/{id}` | PUT aceita `status` (Scheduled/Confirmed/Completed/Cancelled) |
| POST / PUT toggle / DELETE | `/events/{id}/equipment[/{eventEquipmentId}]` | checklist (PRD §14) |
| PUT / DELETE | `/events/{id}/team-shares[/{teamMemberId}]` | divisão sobrescrita (RN013) |
| GET | `/events/{id}/division` | cachê − despesas = distribuível |

## /api/financial (artista)

| Método | Rota | Observação |
|---|---|---|
| GET | `/financial/transactions?type&status&from&to&page&pageSize` | |
| POST | `/financial/transactions` — `{ type, category, amount, dueDate?, eventId?, notes? }` | |
| PUT / DELETE | `/financial/transactions/{id}` | |
| POST | `/financial/transactions/{id}/settle` | recebida/paga |
| GET | `/financial/summary?from&to` | previsto, recebido, despesas, resultado, contas a receber, próximos shows |

## /api/team · /api/equipment (artista)

CRUD padrão: `GET`, `POST`, `PUT /{id}`, `DELETE /{id}`.
- Team: `{ name, role, phone, email, defaultShareType, defaultShareValue, notes }`.
- Equipment: `{ name, category, brand, model, identifier, weightKg?, value?, status, notes }`.

## /api/reviews · /api/favorites · /api/notifications · /api/contractors · /api/uploads

| Método | Rota | Auth | Observação |
|---|---|---|---|
| POST | `/reviews` — `{ eventId, overallRating, punctuality, quality, professionalism, communication, comment }` | Contratante | RN014/RN015/RN016 |
| GET | `/reviews/artist/{artistId}` | — | |
| GET / POST `{artistId}` / DELETE `{artistId}` | `/favorites` | Contratante | |
| GET / PUT `{id}/read` / PUT `read-all` | `/notifications` (+`unreadOnly`) · `/notifications/unread-count` | ✔ | in-app (PRD §23) |
| GET / PUT `/contractors/me` | | Contratante | |
| POST | `/uploads` (multipart `file`) | ✔ | JPEG/PNG/WebP/MP4 ≤ 10 MB → `{ url }` |
