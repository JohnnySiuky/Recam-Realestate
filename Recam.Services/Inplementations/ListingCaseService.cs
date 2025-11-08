using Microsoft.EntityFrameworkCore;
using Recam.Common.Exceptions;
using Recam.Models.Entities;
using Recam.Models.Enums;
using Recam.Respostitories.Interfaces;
using Recam.Services.DTOs;
using Recam.Services.Interfaces;
using System.Linq;
using Recam.Common.Extensions;
using Recam.Services.Logging;
using Recam.Services.Logging.interfaces;

namespace Recam.Services.Email;

public class ListingCaseService : IListingCaseService
{
    private readonly IListingCaseRepository _repo;
    private readonly ICaseHistoryService _history;
    private readonly IMediaRepository _mediaRepo;
    private readonly IMediaAssetRepository _mediaAssetRepository;
    private readonly PublicListingOptions _publicOpt;
    private readonly IListingAuditLogService _listingAudit;
    

    public ListingCaseService(IListingCaseRepository repo, ICaseHistoryService history,  IMediaRepository mediaRepo, IMediaAssetRepository mediaAssetRepository, PublicListingOptions publicOpt, IListingAuditLogService listingAudit)
    {
        _repo = repo;
        _history = history;
        _mediaRepo = mediaRepo;
        _mediaAssetRepository = mediaAssetRepository;
        _publicOpt = publicOpt;
        _listingAudit = listingAudit;
    }

    public async Task<ListingCaseDto> CreateAsync(string currentUserId, bool canCreate, CreateListingCaseRequest req, CancellationToken ct)
    {
        if (!canCreate)
            throw new ForbiddenException("Only Admin or PhotographyCompany can create listing");

        var entity = new ListingCase
        {
            Title = req.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim(),
            Street = req.Street.Trim(),
            City = req.City.Trim(),
            State = req.State.Trim(),
            PostalCode = req.PostalCode,
            PropertyType = req.PropertyType,
            SaleCategory = req.SaleCategory,
            Bedrooms = req.Bedrooms,
            Bathrooms = req.Bathrooms,
            Garages = req.Garages,
            FloorArea = req.FloorArea,
            Price = req.Price,
            Latitude = req.Latitude ?? 0,
            Longitude = req.Longitude ?? 0,
            ListingCaseStatus = ListingCaseStatus.Created, // ★ 初始狀態
            UserId = currentUserId,
            CreatedAt = DateTime.UtcNow
        };

        await _repo.AddAsync(entity, ct);
        await _repo.SaveAsync(ct);

        await _history.CreatedAsync(entity.Id, currentUserId, new
        {
            entity.Title,
            entity.PropertyType,
            entity.SaleCategory,
            entity.Bedrooms,
            entity.Bathrooms
        }, ct);

        return new ListingCaseDto(
            entity.Id,
            entity.Title ?? "",
            entity.Description,
            entity.Street,
            entity.City,
            entity.State,
            entity.PostalCode,
            entity.PropertyType,
            entity.SaleCategory,
            entity.Bedrooms,
            entity.Bathrooms,
            entity.Garages,
            entity.FloorArea,
            entity.Price,
            entity.Latitude,
            entity.Longitude,
            entity.ListingCaseStatus,
            entity.UserId,
            entity.CreatedAt
        );
    }
    
    public async Task<ListingCaseDto> UpdateAsync(int id, string currentUserId, bool isAdmin, UpdateListingCaseRequest req, CancellationToken ct)
    {
        if (!isAdmin)
            throw new ForbiddenException("Only Admin can update a listing.");

        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity is null)
            throw new NotFoundException($"Listing case #{id} not found.");

        if (entity.IsDeleted)
            throw new ConflictException("Listing has been deleted.");

        // 只允許更新屬性資料，不動staus與builder
        var before = new
        {
            entity.Title, entity.Description, entity.Street, entity.City, entity.State, entity.PostalCode,
            entity.PropertyType, entity.SaleCategory, entity.Bedrooms, entity.Bathrooms, entity.Garages,
            entity.FloorArea, entity.Price, entity.Latitude, entity.Longitude
        };

        entity.Title       = req.Title.Trim();
        entity.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
        entity.Street      = req.Street.Trim();
        entity.City        = req.City.Trim();
        entity.State       = req.State.Trim();
        entity.PostalCode  = req.PostalCode;
        entity.PropertyType= req.PropertyType;
        entity.SaleCategory= req.SaleCategory;
        entity.Bedrooms    = req.Bedrooms;
        entity.Bathrooms   = req.Bathrooms;
        entity.Garages     = req.Garages;
        entity.FloorArea   = req.FloorArea;
        entity.Price       = req.Price;
        entity.Latitude    = req.Latitude ?? 0;
        entity.Longitude   = req.Longitude ?? 0;

        _repo.Update(entity);
        await _repo.SaveAsync(ct);

        
        var after = new
        {
            entity.Title, entity.Description, entity.Street, entity.City, entity.State, entity.PostalCode,
            entity.PropertyType, entity.SaleCategory, entity.Bedrooms, entity.Bathrooms, entity.Garages,
            entity.FloorArea, entity.Price, entity.Latitude, entity.Longitude
        };
        var changes = new Dictionary<string, object?>();
        foreach (var prop in after.GetType().GetProperties())
        {
            var newVal = prop.GetValue(after);
            var oldVal = before.GetType().GetProperty(prop.Name)!.GetValue(before);
            if ((newVal is null && oldVal is not null) || (newVal is not null && !newVal.Equals(oldVal)))
                changes[prop.Name] = new { before = oldVal, after = newVal };
        }

        await _history.UpdatedAsync(entity.Id, currentUserId, changes, ct);

        return new ListingCaseDto(
            entity.Id,
            entity.Title ?? "",
            entity.Description,
            entity.Street,
            entity.City,
            entity.State,
            entity.PostalCode,
            entity.PropertyType,
            entity.SaleCategory,
            entity.Bedrooms,
            entity.Bathrooms,
            entity.Garages,
            entity.FloorArea,
            entity.Price,
            entity.Latitude,
            entity.Longitude,
            entity.ListingCaseStatus,
            entity.UserId,
            entity.CreatedAt
        );
    }
    
    public async Task<PagedResult<ListingCaseDto>> GetPagedAsync(
        string currentUserId,
        IReadOnlyCollection<string> roles,
        GetListingCasesQuery req,
        CancellationToken ct)
    {
        var isAdmin = roles.Contains("Admin");
        var isAgent = roles.Contains("Agent");
        var isPhoto = roles.Contains("PhotographyCompany");

        // base query
        var q = _repo.Query();

        // role scope
        if (isAdmin)
        {
            // 看全部
        }
        else if (isAgent)
        {
            var assignedIds = _repo.AgentAssignments()
                .Where(a => a.AgentId == currentUserId)
                .Select(a => a.ListingCaseId);

            q = q.Where(x => assignedIds.Contains(x.Id));
        }
        else if (isPhoto)
        {
            q = q.Where(x => x.UserId == currentUserId);
        }
        else
        {
            // 未知角色：不給資料
            q = q.Where(x => false);
        }

        // filters
        if (!string.IsNullOrWhiteSpace(req.Q))
        {
            var kw = $"%{req.Q.Trim()}%";
            q = q.Where(x =>
                EF.Functions.Like(x.Title ?? "", kw) ||
                EF.Functions.Like(x.Description ?? "", kw) ||
                EF.Functions.Like(x.Street, kw) ||
                EF.Functions.Like(x.City, kw) ||
                EF.Functions.Like(x.State, kw));
        }
        if (!string.IsNullOrWhiteSpace(req.City))  q = q.Where(x => x.City == req.City);
        if (!string.IsNullOrWhiteSpace(req.State)) q = q.Where(x => x.State == req.State);
        if (req.PropertyType.HasValue)             q = q.Where(x => x.PropertyType == req.PropertyType);
        if (req.SaleCategory.HasValue)             q = q.Where(x => x.SaleCategory == req.SaleCategory);
        if (req.Status.HasValue)                   q = q.Where(x => x.ListingCaseStatus == req.Status);
        if (req.MinBedrooms.HasValue)              q = q.Where(x => x.Bedrooms >= req.MinBedrooms);
        if (req.MaxBedrooms.HasValue)              q = q.Where(x => x.Bedrooms <= req.MaxBedrooms);
        if (req.MinPrice.HasValue)                 q = q.Where(x => x.Price >= req.MinPrice);
        if (req.MaxPrice.HasValue)                 q = q.Where(x => x.Price <= req.MaxPrice);
        if (req.CreatedFromUtc.HasValue)           q = q.Where(x => x.CreatedAt >= req.CreatedFromUtc);
        if (req.CreatedToUtc.HasValue)             q = q.Where(x => x.CreatedAt <= req.CreatedToUtc);

        // sorting
        var sortBy  = (req.SortBy ?? "CreatedAt").ToLowerInvariant();
        var sortDir = (req.SortDir ?? "desc").ToLowerInvariant();
        q = (sortBy, sortDir) switch
        {
            ("title", "asc")      => q.OrderBy(x => x.Title),
            ("title", "desc")     => q.OrderByDescending(x => x.Title),
            ("city", "asc")       => q.OrderBy(x => x.City),
            ("city", "desc")      => q.OrderByDescending(x => x.City),
            ("price", "asc")      => q.OrderBy(x => x.Price),
            ("price", "desc")     => q.OrderByDescending(x => x.Price),
            ("bedrooms", "asc")   => q.OrderBy(x => x.Bedrooms),
            ("bedrooms", "desc")  => q.OrderByDescending(x => x.Bedrooms),
            ("createdat", "asc")  => q.OrderBy(x => x.CreatedAt),
            _                     => q.OrderByDescending(x => x.CreatedAt)
        };

        // paging
        var page     = Math.Max(1, req.Page);
        var pageSize = Math.Clamp(req.PageSize, 1, 100);

        var total = await q.CountAsync(ct);
        var items = await q.Skip((page - 1) * pageSize)
                           .Take(pageSize)
                           .Select(x => new ListingCaseDto(
                               x.Id, x.Title ?? "", x.Description,
                               x.Street, x.City, x.State, x.PostalCode,
                               x.PropertyType, x.SaleCategory,
                               x.Bedrooms, x.Bathrooms, x.Garages,
                               x.FloorArea, x.Price, x.Latitude, x.Longitude,
                               x.ListingCaseStatus, x.UserId, x.CreatedAt))
                           .ToListAsync(ct);

        return new PagedResult<ListingCaseDto>(items, total, page, pageSize);
    }
    
    public async Task DeleteAsync(int id, string currentUserId, bool isAdmin, CancellationToken ct)
    {
        if (!isAdmin)
            throw new ForbiddenException("Only Admin can delete a listing.");

        var ok = await _repo.SoftDeleteCascadeAsync(id, ct);
        if (!ok)
            throw new NotFoundException($"Listing case #{id} not found.");

        await _history.DeletedAsync(id, currentUserId, new { Reason = "Admin request" }, ct);
    }
    
    public async Task<ListingCaseDetailDto> GetDetailAsync(
        int id,
        string currentUserId,
        IReadOnlyCollection<string> roles,
        CancellationToken ct)
    {
        var isAdmin = roles.Contains("Admin");
        var isAgent = roles.Contains("Agent");
        var isPhoto = roles.Contains("PhotographyCompany");

        // 權限限定在查詢裡，避免先撈再判斷
        var q = _repo.Query().Where(l => l.Id == id);

        if (isAdmin)
        {
            // 看全部
        }
        else if (isAgent)
        {
            q = q.Where(l => l.AgentListingCases.Any(a => a.AgentId == currentUserId));
        }
        else if (isPhoto)
        {
            q = q.Where(l => l.UserId == currentUserId);
        }
        else
        {
            throw new ForbiddenException("Permission denied.");
        }
        
        var dto = await q.AsNoTracking()
            .Select(l => new ListingCaseDetailDto(
                l.Id,
                l.Title ?? "",
                l.Description,
                l.Street,
                l.City,
                l.State,
                l.PostalCode,
                l.PropertyType,
                l.SaleCategory,
                l.Bedrooms,
                l.Bathrooms,
                l.Garages,
                l.FloorArea,
                l.Price,
                l.Latitude,
                l.Longitude,
                l.ListingCaseStatus,
                l.CreatedAt,
                l.UserId,
                l.User.Email,
                l.Media
                    .Where(m => !m.IsDeleted)
                    .OrderByDescending(m => m.IsHero)
                    .ThenByDescending(m => m.UploadedAt)
                    .Select(m => new MediaItemDto(
                        m.Id, m.MediaType, m.MediaUrl, m.UploadedAt, m.IsSelected, m.IsHero
                    )).ToList(),
                l.AgentListingCases
                    .Select(al => new AgentSummaryDto(
                        al.AgentId,
                        al.Agent.AgentFirstName,
                        al.Agent.AgentLastName,
                        al.Agent.User.Email,          // 若沒有這個導覽屬性，就改為 null 或另外 join
                        al.Agent.CompanyName,
                        al.Agent.AvatarUrl
                    )).ToList()
            ))
            .FirstOrDefaultAsync(ct);

        if (dto is null)
            throw new NotFoundException($"Listing #{id} not found.");

        return dto;
    }
    
    private static readonly IReadOnlyDictionary<ListingCaseStatus, ListingCaseStatus[]> AllowedTransitions =
            new Dictionary<ListingCaseStatus, ListingCaseStatus[]>
            {
                [ListingCaseStatus.Created]  = new[] { ListingCaseStatus.Pending },
                [ListingCaseStatus.Pending]  = new[] { ListingCaseStatus.Delivered },
                [ListingCaseStatus.Delivered]= Array.Empty<ListingCaseStatus>()
            };

    public async Task<ChangeListingStatusResponse> ChangeStatusAsync(
        int id,
        string operatorUserId,
        IReadOnlyCollection<string> roles,
        ListingCaseStatus newStatus,
        string? reason,
        CancellationToken ct)
    {
        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity is null || entity.IsDeleted)
            throw new NotFoundException($"Listing #{id} not found.");

        // 權限：Admin 全部；PhotographyCompany 只能改自己建立的單；Agent 不允許
        var isAdmin = roles.Contains("Admin");
        var isPhoto = roles.Contains("PhotographyCompany");
        if (!(isAdmin || (isPhoto && entity.UserId == operatorUserId)))
            throw new ForbiddenException("You are not allowed to change the status of this listing.");

        var old = entity.ListingCaseStatus;
        if (old == newStatus)
            throw new ConflictException($"Listing #{id} already in status '{old}'.");

        if (!AllowedTransitions.TryGetValue(old, out var nexts) || !nexts.Contains(newStatus))
            throw new ValidationException(
                "INVALID_TRANSITION",
                $"Cannot change status from '{old}' to '{newStatus}'. Allowed next: {string.Join(", ", nexts ?? Array.Empty<ListingCaseStatus>())}");

        entity.ListingCaseStatus = newStatus;
        _repo.Update(entity);
        await _repo.SaveAsync(ct);

        var at = DateTime.UtcNow;
        // 你的 ICaseHistoryService 只有 UpdatedAsync → 用它記錄狀態變更
        await _history.UpdatedAsync(entity.Id, operatorUserId, new
        {
            Type = "StatusChanged",
            OldStatus = old.ToString(),
            NewStatus = newStatus.ToString(),
            Reason = reason,
            ChangedAt = at
        }, ct);

        return new ChangeListingStatusResponse(entity.Id, old, newStatus, at);
    }
    
    public async Task<ListingMediaResponse> GetMediaAsync(
        int listingId,
        string currentUserId,
        IReadOnlyCollection<string> roles,
        CancellationToken ct)
    {
        var isAdmin = roles.Contains("Admin");
        var isAgent = roles.Contains("Agent");
        var isPhoto = roles.Contains("PhotographyCompany");

        // 先確認是否存在（未刪除）
        var exists = await _repo.Query().AsNoTracking().AnyAsync(l => l.Id == listingId, ct);
        if (!exists) throw new NotFoundException($"Listing #{listingId} not found.");

        // 權限限定在查詢裡
        var scope = _repo.Query().Where(l => l.Id == listingId);
        if (isAdmin)
        {
            // 看全部
        }
        else if (isPhoto)
        {
            scope = scope.Where(l => l.UserId == currentUserId);
        }
        else if (isAgent)
        {
            scope = scope.Where(l => l.AgentListingCases.Any(a => a.AgentId == currentUserId));
        }
        else
        {
            throw new ForbiddenException("Permission denied.");
        }

        // 檢查是否真的有權限
        var hasAccess = await scope.AsNoTracking().AnyAsync(ct);
        if (!hasAccess) throw new ForbiddenException("You are not allowed to view media of this listing.");

        // 取出媒體清單（只需要媒體，不用 include，直接投影）
        var flat = await scope
            .SelectMany(l => l.Media.Where(m => !m.IsDeleted))
            .AsNoTracking()
            .Select(m => new MediaItemDto(
                m.Id,
                m.MediaType,
                m.MediaUrl,
                m.UploadedAt,
                m.IsSelected,
                m.IsHero
            ))
            .ToListAsync(ct);

        // 分組 + 每組排序（封面優先，時間新到舊）
        var groups = flat
            .GroupBy(m => m.MediaType)
            .Select(g => new ListingMediaGroupDto(
                g.Key,
                g.OrderByDescending(x => x.IsHero)
                 .ThenByDescending(x => x.UploadedAt)
                 .ToList(),
                g.Count()
            ))
            // 可選：固定分組順序（Picture, Video, FloorPlan, VRTour）
            .OrderBy(g => g.Type)
            .ToList();

        return new ListingMediaResponse(listingId, groups);
    }
    
    public async Task<IReadOnlyList<CaseContactDto>> GetContactsAsync(
        int listingId,
        string currentUserId,
        IReadOnlyCollection<string> roles,
        CancellationToken ct)
    {
        var isAdmin = roles.Contains("Admin");
        var isAgent = roles.Contains("Agent");
        var isPhoto = roles.Contains("PhotographyCompany");

        // 先確認 Listing 存在（且未被軟刪）
        var baseInfo = await _repo.Query()
            .Where(l => l.Id == listingId)
            .Select(l => new
            {
                l.Id,
                l.UserId,
                IsAssignedToAgent = l.AgentListingCases.Any(a => a.AgentId == currentUserId)
            })
            .FirstOrDefaultAsync(ct);

        if (baseInfo is null)
            throw new NotFoundException($"Listing #{listingId} not found.");   // 404

        // 非 Admin 才需要進一步做權限判斷
        if (!isAdmin)
        {
            var canView =
                (isPhoto && baseInfo.UserId == currentUserId) ||
                (isAgent && baseInfo.IsAssignedToAgent);

            if (!canView)
                throw new ForbiddenException("You are not allowed to view contacts of this listing."); // 403
        }

        // 撈聯絡人（照你的欄位）
        var contacts = await _repo.Contacts()
            .Where(c => c.ListingCaseId == listingId)
            .OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
            .Select(c => new CaseContactDto(
                c.ContactId,
                c.FirstName,
                c.LastName,
                c.CompanyName,
                c.ProfileUrl,
                c.Email,
                c.PhoneNumber
            ))
            .ToListAsync(ct);

        return contacts;
    }
    
    public async Task<IReadOnlyList<MediaGroupDto>> GetListingMediaAsync(
        int listingId, string currentUserId, IReadOnlyCollection<string> roles, CancellationToken ct)
    {
        var isAdmin = roles.Contains("Admin");
        var isAgent = roles.Contains("Agent");
        var isPhoto = roles.Contains("PhotographyCompany");

        // 先確認 listing 存在 + 取得最小授權資訊
        var info = await _repo.Query()
            .Where(l => l.Id == listingId)
            .Select(l => new
            {
                l.Id,
                l.UserId,
                IsAssignedToAgent = l.AgentListingCases.Any(a => a.AgentId == currentUserId)
            })
            .FirstOrDefaultAsync(ct);

        if (info is null)
            throw new NotFoundException($"Listing #{listingId} not found.");

        if (!isAdmin)
        {
            var allowed =
                (isPhoto && info.UserId == currentUserId) ||
                (isAgent && info.IsAssignedToAgent);

            if (!allowed)
                throw new ForbiddenException("You are not allowed to view media of this listing.");
        }

        // 取媒體，過濾已刪除；排序：Hero > Selected > UploadedAt desc
        var items = await _mediaRepo.Query()
            .Where(m => m.ListingCaseId == listingId && !m.IsDeleted)
            .OrderByDescending(m => m.IsHero)
            .ThenByDescending(m => m.IsSelected)
            .ThenByDescending(m => m.UploadedAt)
            .Select(m => new MediaItemDto(
                m.Id, m.MediaType, m.MediaUrl, m.UploadedAt, m.IsSelected, m.IsHero
            ))
            .ToListAsync(ct);

        var grouped = items
            .GroupBy(i => i.MediaType)
            .Select(g => new MediaGroupDto(g.Key, g.ToList()))
            .OrderBy(g => g.MediaType) // 穩定輸出
            .ToList();

        return grouped;
    }
    
    public async Task<CaseContactDto> AddContactAsync(
        int listingId,
        string currentUserId,
        IReadOnlyCollection<string> roles,
        AddCaseContactRequest req,
        CancellationToken ct)
    {
        var isAdmin = roles.Contains("Admin");
        var isAgent = roles.Contains("Agent");
        var isPhoto = roles.Contains("PhotographyCompany");

        // 先抓 listing 的 owner（只取必要欄位）
        var listingOwner = await _repo.Query()
            .Where(l => l.Id == listingId)
            .Select(l => new { l.Id, l.UserId })
            .FirstOrDefaultAsync(ct);

        if (listingOwner is null)
            throw new NotFoundException($"Listing #{listingId} not found.");

        // 權限：Admin 任意；Agent 需被指派；PhotographyCompany 需為建立者
        if (isAdmin)
        { /* pass */ }
        else if (isAgent)
        {
            var assigned = await _repo.IsAgentAssignedAsync(listingId, currentUserId, ct);
            if (!assigned) throw new ForbiddenException("You are not assigned to this listing.");
        }
        else if (isPhoto)
        {
            if (listingOwner.UserId != currentUserId)
                throw new ForbiddenException("You are not the owner of this listing.");
        }
        else
        {
            throw new ForbiddenException("Permission denied.");
        }

        // 去重（以 Email 為 listing 內唯一）
        var email = req.Email.Trim().ToLowerInvariant();
        if (await _repo.ContactExistsAsync(listingId, email, ct))
            throw new ConflictException("A contact with the same email already exists in this listing.");

        var entity = new CaseContact
        {
            ListingCaseId = listingId,
            FirstName = req.FirstName.Trim(),
            LastName  = req.LastName.Trim(),
            Email     = email,
            PhoneNumber = req.PhoneNumber.Trim(),
            CompanyName = string.IsNullOrWhiteSpace(req.CompanyName) ? null : req.CompanyName.Trim(),
            ProfileUrl  = string.IsNullOrWhiteSpace(req.ProfileUrl)  ? null : req.ProfileUrl.Trim()
        };

        await _repo.AddContactAsync(entity, ct);
        await _repo.SaveAsync(ct);

        return new CaseContactDto(
            ContactId:   entity.ContactId,
            FirstName:   entity.FirstName,
            LastName:    entity.LastName,
            CompanyName: entity.CompanyName,
            ProfileUrl:  entity.ProfileUrl,
            Email:       entity.Email,
            PhoneNumber: entity.PhoneNumber
        );
    }
    
    public async Task<SetCoverImageResponse> SetCoverImageAsync(
        int listingId,
        int mediaId,
        string operatorUserId,
        CancellationToken ct = default)
    {
        // 1) 取 Listing
        var listing = await _repo.GetByIdAsync(listingId, ct);
        if (listing is null || listing.IsDeleted)
            throw new NotFoundException("Listing not found.");

        // 2) 取 Media，并校验归属、类型（必须是 Photo）
        var media = await _mediaRepo.GetByIdAsync(mediaId, ct)
                    ?? throw new NotFoundException("Media not found.");
        if (media.ListingCaseId != listingId || media.IsDeleted)
            throw new BadRequestException("The media is not part of this listing.");
        if (media.MediaType != MediaType.Photo)
            throw new BadRequestException("Cover image must be a Photo media.");

        // 3) 更新封面（Listing.CoverImageUrl）+ 维护 Hero
        listing.CoverImageUrl = media.MediaUrl;
        _repo.Update(listing);
        await _repo.SaveAsync(ct);

        await _mediaAssetRepository.ClearHeroAsync(listingId, ct);
        await _mediaAssetRepository.SetHeroAsync(mediaId, ct);
        await _mediaAssetRepository.SaveAsync(ct);

        // 4) 记历史（Mongo 已禁用则 Noop，不会 500）
        await _history.UpdatedAsync(listingId, operatorUserId, new
        {
            action = "COVER_IMAGE_SET",
            mediaId,
            url = media.MediaUrl
        }, ct);

        return new SetCoverImageResponse(listingId, mediaId, media.MediaUrl);
    }
    
    public async Task<PublishListingResponse> PublishAsync(
    int listingId,
    string currentUserId,
    IReadOnlyCollection<string> roles,
    CancellationToken ct = default)
{
    var isAdmin   = roles.Contains("Admin");
    var isCompany = roles.Contains("PhotographyCompany");

    if (!isAdmin && !isCompany)
        throw new ForbiddenException("Only Admin or PhotographyCompany can publish listings.");

    // 用 tracking 实体
    var listing = await _repo.GetForUpdateAsync(listingId, ct);
    if (listing is null)
        throw new NotFoundException("Listing not found.");

    if (isCompany && !isAdmin && listing.UserId != currentUserId)
        throw new ForbiddenException("You are not allowed to publish this listing.");

    if (listing.IsDeleted)
        throw new BadRequestException("Cannot publish a deleted listing.");

    // 如果还没有公开链接 -> 生成一个
    if (string.IsNullOrWhiteSpace(listing.PublicUrl))
    {
        var token    = Guid.NewGuid().ToString("N")[..10]; // 简短 ID
        var baseUrl  = _publicOpt.BaseUrl?.TrimEnd('/') ?? string.Empty;
        var finalUrl = $"{baseUrl}/{token}";

        listing.PublicUrl = finalUrl;

        await _repo.SaveAsync(ct);

        // 写 publish 日志
        await _listingAudit.LogAsync(new ListingPublishLog
        {
            ListingCaseId  = listingId,
            OperatorUserId = currentUserId,
            PublicUrl      = finalUrl,
            TimestampUtc   = DateTime.UtcNow,
            Event          = "LISTING_PUBLISHED"
        }, ct);
    }
    else
    {
        // 已经有链接了，再按 publish 也要记一笔
        await _listingAudit.LogAsync(new ListingPublishLog
        {
            ListingCaseId  = listingId,
            OperatorUserId = currentUserId,
            PublicUrl      = listing.PublicUrl!,
            TimestampUtc   = DateTime.UtcNow,
            Event          = "LISTING_PUBLISHED_AGAIN"
        }, ct);
    }

    return new PublishListingResponse(
        ListingCaseId: listingId,
        PublicUrl: listing.PublicUrl!
    );
}
}