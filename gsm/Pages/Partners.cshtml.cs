using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using System.Text.Json;
using gsm.Data;
using gsm.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace gsm.Pages;

[Authorize(Roles = "Boss")]
public class PartnersModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly TenantContext _tenantContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly IDataProtector _protector;

    public PartnersModel(
        ApplicationDbContext dbContext,
        TenantContext tenantContext,
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender,
        IDataProtectionProvider dataProtectionProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _userManager = userManager;
        _emailSender = emailSender;
        _protector = dataProtectionProvider.CreateProtector("gsm.BusinessPartnerInvitation");
    }

    [BindProperty]
    public InviteInput Input { get; set; } = new();

    public List<ConnectionView> Connections { get; private set; } = [];
    public List<IncomingInvitationView> IncomingInvitations { get; private set; } = [];
    public PendingInvitation? IncomingInvitation { get; private set; }
    public string? StatusMessage { get; private set; }

    public async Task OnGetAsync(string? invite = null)
    {
        await LoadConnectionsAsync();
        if (!string.IsNullOrWhiteSpace(invite))
            IncomingInvitation = await ReadInvitationAsync(invite);
    }

    public async Task<IActionResult> OnPostInviteAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadConnectionsAsync();
            return Page();
        }

        var ownerCompanyId = _tenantContext.CompanyId;
        if (string.IsNullOrWhiteSpace(ownerCompanyId)) return Forbid();
        var target = await _userManager.Users.FirstOrDefaultAsync(user => user.Email == Input.Email!.Trim());
        if (target == null || string.IsNullOrWhiteSpace(target.CompanyId) || !await _userManager.IsInRoleAsync(target, "Boss"))
        {
            ModelState.AddModelError("Input.Email", "A Boss account with this email was not found.");
            await LoadConnectionsAsync();
            return Page();
        }
        if (target.CompanyId == ownerCompanyId)
        {
            ModelState.AddModelError("Input.Email", "You cannot invite a Boss from your own company.");
            await LoadConnectionsAsync();
            return Page();
        }

        var exists = await _dbContext.BusinessPartnerConnections.AnyAsync(connection =>
            ((connection.OwnerCompanyId == ownerCompanyId && connection.PartnerCompanyId == target.CompanyId) ||
             (connection.OwnerCompanyId == target.CompanyId && connection.PartnerCompanyId == ownerCompanyId)) &&
            connection.Status != BusinessPartnerConnectionStatus.Rejected);
        if (exists)
        {
            ModelState.AddModelError("Input.Email", "A partnership invitation already exists for this company.");
            await LoadConnectionsAsync();
            return Page();
        }

        var connection = new BusinessPartnerConnection
        {
            OwnerCompanyId = ownerCompanyId,
            PartnerCompanyId = target.CompanyId,
            InvitedEmail = target.Email!,
            InvitedByUserId = _userManager.GetUserId(User)!
        };
        _dbContext.BusinessPartnerConnections.Add(connection);
        await _dbContext.SaveChangesAsync();

        var payload = JsonSerializer.Serialize(new { connection.Id, TargetUserId = target.Id });
        var token = WebEncoders.Base64UrlEncode(_protector.Protect(Encoding.UTF8.GetBytes(payload)));
        var invitationUrl = Url.Page("/Partners", null, new { invite = token }, Request.Scheme);
        var ownerCompany = await _dbContext.Companies.FirstAsync(company => company.Id == ownerCompanyId);
        await _emailSender.SendEmailAsync(
            target.Email!,
            "Business partnership invitation",
            $"Hello {WebUtility.HtmlEncode(target.CustomerName ?? target.Email)},<br><br>" +
            $"<b>{WebUtility.HtmlEncode(ownerCompany.Name)}</b> invited your company to become a business partner.<br><br>" +
            $"<a href='{WebUtility.HtmlEncode(invitationUrl)}'>Open the invitation</a> and accept it from the Partners page.");

        TempData["StatusMessage"] = "Partnership invitation sent.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAcceptConnectionAsync(int id)
    {
        var companyId = _tenantContext.CompanyId;
        var connection = await _dbContext.BusinessPartnerConnections.FirstOrDefaultAsync(item =>
            item.Id == id && item.PartnerCompanyId == companyId && item.Status == BusinessPartnerConnectionStatus.Pending);
        if (connection == null) return NotFound();
        connection.Status = BusinessPartnerConnectionStatus.Accepted;
        connection.AcceptedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        TempData["StatusMessage"] = "Business partnership accepted.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAcceptAsync(string token)
    {
        var invitation = await ReadInvitationAsync(token);
        var companyId = _tenantContext.CompanyId;
        if (invitation == null || string.IsNullOrWhiteSpace(companyId) || invitation.PartnerCompanyId != companyId || invitation.TargetUserId != _userManager.GetUserId(User))
            return Forbid();

        var connection = await _dbContext.BusinessPartnerConnections.FirstOrDefaultAsync(item =>
            item.Id == invitation.Id && item.PartnerCompanyId == companyId && item.Status == BusinessPartnerConnectionStatus.Pending);
        if (connection == null) return NotFound();
        connection.Status = BusinessPartnerConnectionStatus.Accepted;
        connection.AcceptedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        TempData["StatusMessage"] = "Business partnership accepted.";
        return RedirectToPage();
    }

    private async Task LoadConnectionsAsync()
    {
        var companyId = _tenantContext.CompanyId;
        if (string.IsNullOrWhiteSpace(companyId)) return;
        var connections = await _dbContext.BusinessPartnerConnections
            .IgnoreQueryFilters()
            .Where(connection => connection.OwnerCompanyId == companyId || connection.PartnerCompanyId == companyId)
            .OrderByDescending(connection => connection.CreatedAt)
            .ToListAsync();
        var companyIds = connections.SelectMany(connection => new[] { connection.OwnerCompanyId, connection.PartnerCompanyId }).Distinct().ToList();
        var companies = await _dbContext.Companies.Where(company => companyIds.Contains(company.Id)).ToDictionaryAsync(company => company.Id, company => company.Name);
        Connections = connections.Select(connection => new ConnectionView(
            connection.Id,
            companies.GetValueOrDefault(connection.OwnerCompanyId, "Company"),
            companies.GetValueOrDefault(connection.PartnerCompanyId, "Company"),
            connection.OwnerCompanyId == companyId ? "Outgoing" : "Incoming",
            connection.Status.ToString())).ToList();
        IncomingInvitations = connections
            .Where(connection => connection.PartnerCompanyId == companyId && connection.Status == BusinessPartnerConnectionStatus.Pending)
            .Select(connection => new IncomingInvitationView(connection.Id, companies.GetValueOrDefault(connection.OwnerCompanyId, "Company")))
            .ToList();
    }

    private async Task<PendingInvitation?> ReadInvitationAsync(string token)
    {
        try
        {
            var json = Encoding.UTF8.GetString(_protector.Unprotect(WebEncoders.Base64UrlDecode(token)));
            var payload = JsonSerializer.Deserialize<InvitationPayload>(json);
            if (payload == null) return null;
            var connection = await _dbContext.BusinessPartnerConnections
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(item => item.Id == payload.Id && item.Status == BusinessPartnerConnectionStatus.Pending);
            if (connection == null) return null;
            var companies = await _dbContext.Companies
                .Where(company => company.Id == connection.OwnerCompanyId || company.Id == connection.PartnerCompanyId)
                .ToDictionaryAsync(company => company.Id, company => company.Name);
            return new PendingInvitation(connection.Id, connection.OwnerCompanyId, connection.PartnerCompanyId, payload.TargetUserId, companies.GetValueOrDefault(connection.OwnerCompanyId, "Company"));
        }
        catch
        {
            return null;
        }
    }

    public sealed class InviteInput
    {
        [Required, EmailAddress]
        public string? Email { get; set; }
    }

    public sealed record ConnectionView(int Id, string OwnerCompany, string PartnerCompany, string Direction, string Status);
    public sealed record IncomingInvitationView(int Id, string OwnerCompanyName);
    public sealed record PendingInvitation(int Id, string OwnerCompanyId, string PartnerCompanyId, string TargetUserId, string OwnerCompanyName);
    private sealed record InvitationPayload(int Id, string TargetUserId);
}
