using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recam.Common.Exceptions;
using Recam.Models.Entities;
using Recam.Models.Enums;
using Recam.Respostitories.Interfaces;
using Recam.Services.DTOs;
using Recam.Services.Interfaces;
using Recam.Services.Logging.interfaces;

namespace Recam.Services.Email;

public class FinalSelectionService : IFinalSelectionService
{
        private readonly IListingCaseRepository _listingRepo;
        private readonly ISelectedMediaRepository _selectedRepo;
        private readonly IMediaAssetRepository _mediaRepo;
        private readonly IMediaSelectionLogService _logSvc; // Mongo logger
        private readonly ILogger<FinalSelectionService> _logger;

        public FinalSelectionService(
            IListingCaseRepository listingRepo,
            ISelectedMediaRepository selectedRepo,
            IMediaAssetRepository mediaRepo,
            IMediaSelectionLogService logSvc,
            ILogger<FinalSelectionService> logger)
        {
            _listingRepo = listingRepo;
            _selectedRepo = selectedRepo;
            _mediaRepo = mediaRepo;
            _logSvc = logSvc;
            _logger = logger;
        }

        // ========= READ =========
        public async Task<FinalSelectionResponse> GetAsync(
        int listingId,
        string currentUserId,
        IReadOnlyCollection<string> roles,
        CancellationToken ct = default)
    {
        // 读 listing 状态
        var listing = await _listingRepo.Query()
            .AsNoTracking()
            .Where(l => l.Id == listingId && !l.IsDeleted)
            .Select(l => new { l.Id, l.ListingCaseStatus })
            .SingleOrDefaultAsync(ct);

        if (listing is null)
            throw new NotFoundException("Listing not found.");

        var isAdmin = roles.Contains("Admin");
        var isAgent = roles.Contains("Agent");

        if (!isAdmin && !isAgent)
            throw new ForbiddenException("Only Admin or Agent can view final selection.");

        if (isAgent)
        {
            var assigned = await _listingRepo.AgentAssignments()
                .AnyAsync(a => a.ListingCaseId == listingId && a.AgentId == currentUserId, ct);
            if (!assigned)
                throw new ForbiddenException("You are not assigned to this listing.");
        }

        if (listing.ListingCaseStatus != ListingCaseStatus.Delivered)
            throw new BadRequestException(
                $"Final selection is not available for a '{listing.ListingCaseStatus}' listing.");

        var rows = await _selectedRepo.Query()
            .AsNoTracking()
            .Where(x => x.ListingCaseId == listingId && x.IsFinal)
            .Select(x => new
            {
                x.MediaAssetId,
                x.SelectedAt,
                x.AgentId,
                x.MediaAsset.MediaType,
                x.MediaAsset.MediaUrl,
                x.MediaAsset.IsHero,
                x.MediaAsset.IsDeleted
            })
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.SelectedAt)
            .ToListAsync(ct);

        var items = rows.Select(x => new SelectedMediaItemDto(
            x.MediaAssetId,
            x.MediaType,
            x.MediaUrl,
            x.IsHero,
            x.SelectedAt,
            x.AgentId
        )).ToList();

        return new FinalSelectionResponse(
            ListingCaseId: listing.Id,
            Status: listing.ListingCaseStatus,
            Count: items.Count,
            Items: items
        );
    }

    // --- 新的: Admin/Agent 提交选择 ---
    public async Task SaveAgentSelectionAsync(
        int listingId,
        string agentUserId,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<int> selectedMediaIds,
        bool markFinal,
        CancellationToken ct = default)
    {
        if (selectedMediaIds == null || selectedMediaIds.Count == 0)
            throw new BadRequestException("Please provide at least one mediaAssetId.");

        var listing = await _listingRepo.Query()
            .Where(l => l.Id == listingId && !l.IsDeleted)
            .Select(l => new { l.Id, l.UserId, l.ListingCaseStatus })
            .SingleOrDefaultAsync(ct);

        if (listing is null)
            throw new NotFoundException("Listing not found.");

        var isAdmin = roles.Contains("Admin");
        var isAgent = roles.Contains("Agent");

        // 权限规则：
        // - Admin: 允许
        // - Agent: 必须被分配到这个 listing
        if (!isAdmin && !isAgent)
            throw new ForbiddenException("Only Admin or Agent can update selection.");

        if (isAgent)
        {
            var assigned = await _listingRepo.AgentAssignments()
                .AnyAsync(a => a.ListingCaseId == listingId && a.AgentId == agentUserId, ct);
            if (!assigned)
                throw new ForbiddenException("You are not assigned to this listing.");
        }

        // 校验 media 是否都属于这个 listing，且没被软删
        var medias = await _mediaRepo.GetByIdsAsync(selectedMediaIds, ct);

        if (medias.Count != selectedMediaIds.Count)
            throw new BadRequestException("One or more mediaAssetIds are invalid or deleted.");

        if (medias.Any(m => m.ListingCaseId != listingId))
            throw new BadRequestException("Some media do not belong to this listing.");

        // 先清掉这个 listing 当前所有 final/selection（不只这个 agent，因为 Admin 可能在 override 全部最终稿）
        await _selectedRepo.DeleteByListingAsync(listingId, ct);

        var now = DateTime.UtcNow;

        var rowsToInsert = medias.Select(m => new SelectedMedia
        {
            ListingCaseId = listingId,
            MediaAssetId  = m.Id,
            AgentId       = isAgent ? agentUserId : null, // 🔥 Admin 提交时写 null
            SelectedAt    = now,
            IsFinal       = markFinal
        }).ToList();

        await _selectedRepo.AddRangeAsync(rowsToInsert, ct);
        await _selectedRepo.SaveAsync(ct);

        // 记录 Mongo/日志，谁下的单
        await _logSvc.LogSelectionAsync(
            listingCaseId: listingId,
            agentUserId: agentUserId,          // 这里我们仍记录谁发的请求（即使是 Admin）
            mediaAssetIds: selectedMediaIds,
            ct: ct);

        _logger.LogInformation(
            "User {User} ({Role}) updated selection for listing {ListingId}. {Count} media. Final={Final}",
            agentUserId,
            isAdmin ? "Admin" : "Agent",
            listingId,
            selectedMediaIds.Count,
            markFinal
        );
    }
}