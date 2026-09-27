# Database Specification — Artist Platform

PostgreSQL 18 + Entity Framework Core 10 (Npgsql). Migrations em
`backend/src/ArtistPlatform.Infrastructure/Migrations`.

- Chaves primárias: `uuid` (GUID).
- Timestamps `CreatedAt`/`UpdatedAt` em todas as entidades (`timestamp with time zone` UTC).
- Enums armazenados como `INT` no banco e serializados como string na API.
- `DateOnly` → `date`, `TimeOnly` → `time without time zone`, `List<string>` → texto JSON (convenções em `AppDbContext`).
- Dinheiros: `numeric(12,2)`.

## Identidade e Acesso (ASP.NET Core Identity)

Tabelas padrão do Identity (`AspNetUsers`, `AspNetRoles`, ...) com chave GUID.
`AspNetUsers` é estendida com: `DisplayName`, `Role` (`UserRole`), `ArtistId?`, `ContractorId?`.

### RefreshTokens
| Coluna | Tipo | Observação |
|---|---|---|
| UserId | char(36) | FK lógica p/ usuário; índice |
| Token | varchar(200) | **unique** |
| ExpiresAt | datetime(6) | |
| RevokedAt | datetime(6)? | rotação de refresh token |
| ReplacedByToken | varchar(200)? | |

## Artistas

### Artists (perfil operacional)
| Coluna | Tipo | Observação |
|---|---|---|
| UserId | char(36) | **unique** — 1 usuário = 1 artista (RN001) |
| Type | int | Solo / Band (RN002: banda tem 1 responsável) |
| Name | varchar(160) | |
| ArtisticName | varchar(160) | |
| CategoryId | char(36) | FK → ArtistCategories |
| City / State / Phone | varchar(120/2/20) | |
| ProfilePhotoUrl | varchar(500) | |

### ArtistProfiles (dados públicos — /artistas/{slug})
| Coluna | Tipo | Observação |
|---|---|---|
| ArtistId | char(36) | **unique** (1:1) |
| Slug | varchar(200) | **unique** |
| Description | varchar(4000) | |
| Styles / Specialties / GalleryUrls / VideoUrls | longtext (JSON) | |
| IsPublished | bool | publicação no marketplace |

### ArtistCategories
`Name` varchar(80), `Slug` varchar(80) **unique**, `IsActive`. Seed: Cantor, Banda, DJ,
Comediante, Stand-up, Músico, Ator, Dançarino, Mágico, Outro.

### ArtistAvailabilities
| Coluna | Tipo | Observação |
|---|---|---|
| ArtistId | char(36) | índice (ArtistId, Date) |
| Date | date | |
| StartTime / EndTime | time | múltiplos intervalos por dia |
| IsAllDay | bool | dia inteiro |
| Status | int | Available / PreReserved / Unavailable |

### ArtistFees (cachês — RN004–RN006)
`ArtistId` (índice), `DurationMinutes` int, `Price` decimal(12,2), `Description`.

## Contratantes

### Contractors
`UserId` **unique**, `Type` (Person/Company), `Name`, `CompanyName?`, `Phone`, `City`, `State`.

## Contratação (ciclo PRD §11)

### BookingRequests
| Coluna | Tipo |
|---|---|
| ArtistId / ContractorId | char(36) — índices (ArtistId, Status) e (ContractorId, Status) |
| EventDate | date |
| StartTime / EndTime | time |
| Location | varchar(300) |
| EventType | int (Show/Rehearsal/...) |
| EstimatedAudience | int? |
| Budget | decimal(12,2)? |
| Message | varchar(2000) |
| Status | int (Requested→Negotiating→Proposed→Accepted→Confirmed→Completed / Cancelled/Rejected/Expired) |
| EventId / AcceptedProposalId | char(36)? |

### Proposals + ProposalItems
- Proposal: `BookingRequestId` (índice), `Status`, `TravelCost`, `EquipmentCost`, `Discount`,
  `FinalAmount` (todos decimal(12,2)), `ValidUntil`, `Notes`, `RespondedAt?`.
- ProposalItem: `ProposalId` FK cascade, `Description`, `Amount`.

### Conversas
- Conversations: `BookingRequestId` **unique** (1 conversa por contratação), `ArtistUserId`,
  `ContractorUserId`, `LastMessageAt?`.
- Messages: `ConversationId` FK cascade, índice (ConversationId, CreatedAt), `SenderUserId`,
  `Content` varchar(4000), `IsRead`, `ReadAt?`.

## Agenda e Eventos

### Events
`ArtistId`, `ContractorId?`, `BookingRequestId?`, `Title`, `Type` (Show/Rehearsal/Meeting/Travel/
Recording/Other), `StartDateTime`, `EndDateTime`, índice (ArtistId, StartDateTime),
`Status` (Scheduled/Confirmed/Completed/Cancelled).
Regra: eventos `Scheduled`/`Confirmed` bloqueiam agenda (RN008/RN009).

## Financeiro (PRD §12)

### FinancialTransactions
`ArtistId`, `Type` (Income/Expense), `Category`, `Amount` decimal(12,2), `DueDate?`,
`SettlementDate?` (recebida/paga), `Status` (Pending/Received/Paid/Overdue/Cancelled),
`EventId?`, índices (ArtistId, Type) e (ArtistId, Status).

## Equipe e Equipamentos

### TeamMembers (RN003: sem contas)
`ArtistId`, `Name`, `PhotoUrl`, `Role`, `Phone`, `Email`, `DefaultShareType` (Percentage/Fixed),
`DefaultShareValue` decimal(12,2), `Notes`.

### EventTeamShares (RN013: sobrescrita por evento)
`EventId`, `TeamMemberId`, `ShareType`, `ShareValue` — **unique (EventId, TeamMemberId)**.

### Equipment + EventEquipment
- Equipment: `ArtistId`, `Name`, `Category`, `Brand`, `Model`, `Identifier`, `WeightKg` decimal(8,2)?,
  `Value` decimal(12,2)?, `Status` (Available/InUse/Maintenance/Unavailable), `Notes`.
- EventEquipment: `EventId`, `EquipmentId`, `IsChecked`, `Notes` — **unique (EventId, EquipmentId)**.

## Avaliações, Favoritos e Notificações

### Reviews (RN014–RN016)
`ArtistId` (índice), `ContractorId`, `EventId` — **unique (EventId, ContractorId)** —
`OverallRating`, `Punctuality`, `Quality`, `Professionalism`, `Communication` (1–5), `Comment`.

### Favorites
`ContractorId`, `ArtistId` — **unique (ContractorId, ArtistId)**.

### Notifications
`UserId`, `Type`, `Title` varchar(200), `Message` varchar(500), `Link?`,
índice (UserId, IsRead).

## Seed (DbInitializer)

- Roles: `Artist`, `Contractor`, `Admin`.
- Categorias (10).
- Admin: `admin@artistplatform.com` / `Admin@123`.
