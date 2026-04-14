using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyWeb.Data;
using MyWeb.Models;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MyWeb.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AuditController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AuditController> _logger;

        public AuditController(ApplicationDbContext context, ILogger<AuditController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Audit/Index
        public async Task<IActionResult> Index(string userId = null, string actionFilter = null, int page = 1)
        {
            try
            {
                int pageSize = 50;
                var query = _context.AuditLogs.AsQueryable();

                _logger.LogInformation($"Loading audit logs - UserId filter: {userId ?? "None"}, Action filter: {actionFilter ?? "None"}");

                if (!string.IsNullOrEmpty(userId))
                {
                    query = query.Where(a => a.EntityId == userId);
                    ViewBag.UserId = userId;
                }

                if (!string.IsNullOrEmpty(actionFilter))
                {
                    query = query.Where(a => a.Action == actionFilter);
                    ViewBag.ActionFilter = actionFilter;
                }

                var totalItems = await query.CountAsync();
                _logger.LogInformation($"Found {totalItems} audit logs matching filters");

                var auditLogs = await query
                    .OrderByDescending(a => a.Timestamp)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                _logger.LogInformation($"Returning {auditLogs.Count} audit logs for page {page}");

                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = (int)System.Math.Ceiling(totalItems / (double)pageSize);
                ViewBag.TotalItems = totalItems;

                return View(auditLogs);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error loading audit logs");
                TempData["ErrorMessage"] = $"Error loading audit logs: {ex.Message}";
                return View(new System.Collections.Generic.List<AuditLog>());
            }
        }

        // GET: Audit/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var auditLog = await _context.AuditLogs.FindAsync(id);
            if (auditLog == null)
            {
                return NotFound();
            }

            return View(auditLog);
        }
    }
}