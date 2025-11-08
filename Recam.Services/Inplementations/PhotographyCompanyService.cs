using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Recam.Common.Exceptions;
using Recam.Models.Entities;
using Recam.Respostitories.Interfaces;
using Recam.Services.DTOs;
using Recam.Services.Interfaces;

namespace Recam.Services.Email;

public class PhotographyCompanyService : IPhotographyCompanyService
{
    private readonly IPhotographyCompanyRepository _repo;
    private readonly IAgentRepository _agents;
    private readonly IIdentityUserService _identity;
    

    public PhotographyCompanyService(IPhotographyCompanyRepository repo, IAgentRepository agents, IIdentityUserService identity)
    {
        _repo = repo;
        _agents = agents;
        _identity = identity;
    }

    public Task<IReadOnlyList<PhotographyCompanyListItemDto>> GetAllAsync(CancellationToken ct = default) =>
        _repo.GetAllAsync(
            c => new PhotographyCompanyListItemDto(c.Id, c.PhotographyCompanyName, c.AgentPhotographyCompanies.Count),
            ct).ContinueWith(t => (IReadOnlyList<PhotographyCompanyListItemDto>)t.Result, ct);
    
    public async Task EnsureForOwnerAsync(string ownerUserId, string name, CancellationToken ct = default)
    {
        if (await _repo.ExistsByOwnerAsync(ownerUserId, ct)) return;

        await _repo.AddAsync(new PhotographyCompany
        {
            Id = ownerUserId,                      // PK + FK 到 ApplicationUser.Id
            PhotographyCompanyName = name
        }, ct);

        await _repo.SaveChangesAsync(ct);
    }
    
    public async Task<AddAgentResponse> AddAgentAsync(
        string currentUserId,
        IReadOnlyCollection<string> roles,
        string agentEmail,
        string? companyId,
        CancellationToken ct)
    {
        // 判斷要綁定的公司
        string targetCompanyId =
            roles.Contains("PhotographyCompany") ? currentUserId :
            roles.Contains("Admin") ? companyId ?? throw new BadRequestException("companyId is required for Admin.") :
            throw new ForbiddenException("Only Admin or PhotographyCompany can add agents.");

        // 找 Agent 使用者
        var agentUser = await _identity.FindByEmailAsync(agentEmail);
        if (agentUser is null) throw new NotFoundException("Agent user not found by email.");

        // 必須是 Agent 角色
        if (!await _identity.IsInRoleAsync(agentUser, "Agent"))
            throw new BadRequestException("The user is not an Agent.");

        // 必須有 Agent Profile
        if (!await _agents.AgentProfileExistsAsync(agentUser.Id, ct))
            throw new BadRequestException("Agent profile not found. Please create Agent profile first.");

        // 檢查關聯是否已存在
        if (await _repo.RelationExistsAsync(agentUser.Id, targetCompanyId, ct))
            throw new BadRequestException("This agent has already been added to the company.");

        // 建立關聯
        await _repo.AddRelationAsync(agentUser.Id, targetCompanyId, ct);
        await _repo.SaveAsync(ct);

        return new AddAgentResponse(agentUser.Id, targetCompanyId);
    }
    
    public async Task<IReadOnlyList<AgentListItemDto>> GetMyAgentsAsync(
        string currentUserId, CancellationToken ct)
    {
        var q =
            _repo.Links() 
                .Where(l => l.PhotographyCompanyId == currentUserId)
                .Join(_agents.Query(),               // ← 用 _agents，不是 _agentRepo
                    l => l.AgentId,
                    a => a.Id,
                    (l, a) => a)
                .OrderBy(a => a.AgentLastName)
                .ThenBy(a => a.AgentFirstName)
                .Select(a => new AgentListItemDto(
                    a.Id,
                    a.AgentFirstName,
                    a.AgentLastName,
                    a.User.Email,     // 有導覽屬性時，EF 會自動產生 JOIN
                    a.CompanyName,
                    a.AvatarUrl
                ));

        return await q.ToListAsync(ct);
    }
}