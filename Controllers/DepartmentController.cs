using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyWeb.Data;
using MyWeb.Models;
using MyWeb.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MyWeb.Controllers
{
    [Authorize(Roles = "Admin")]
    public class DepartmentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;

        public DepartmentController(ApplicationDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        // GET: Department/Index
        public async Task<IActionResult> Index()
        {
            var departments = await _context.Departments
                .OrderBy(d => d.Name)
                .ToListAsync();

            // Count users in each department
            var departmentStats = departments.Select(d => new DepartmentViewModel
            {
                Id = d.Id,
                Name = d.Name,
                Description = d.Description,
                IsActive = d.IsActive,
                CreatedDate = d.CreatedDate,
                UserCount = _context.Users.Count(u => u.Department == d.Name)
            }).ToList();

            return View(departmentStats);
        }

        // GET: Department/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Department/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Department department)
        {
            if (ModelState.IsValid)
            {
                // Check for duplicate
                if (await _context.Departments.AnyAsync(d => d.Name == department.Name))
                {
                    ModelState.AddModelError("Name", "Department already exists.");
                    return View(department);
                }

                department.CreatedBy = User.Identity.Name;
                department.CreatedDate = DateTime.UtcNow;
                department.IsActive = true;

                _context.Departments.Add(department);
                await _context.SaveChangesAsync();

                await _auditService.LogAsync(
                    "Department Created",
                    "Department",
                    department.Id.ToString(),
                    null,
                    null,
                    department.Name,
                    $"Department '{department.Name}' created by {User.Identity.Name}"
                );

                TempData["SuccessMessage"] = $"Department '{department.Name}' created successfully.";
                return RedirectToAction(nameof(Index));
            }

            return View(department);
        }

        // GET: Department/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var department = await _context.Departments.FindAsync(id);
            if (department == null)
            {
                return NotFound();
            }

            return View(department);
        }

        // POST: Department/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Department department)
        {
            if (id != department.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var existingDept = await _context.Departments.FindAsync(id);
                if (existingDept == null)
                {
                    return NotFound();
                }

                // Check for duplicate name (excluding current)
                if (await _context.Departments.AnyAsync(d => d.Name == department.Name && d.Id != id))
                {
                    ModelState.AddModelError("Name", "Department name already exists.");
                    return View(department);
                }

                // Log changes
                if (existingDept.Name != department.Name)
                {
                    await _auditService.LogAsync(
                        "Department Updated",
                        "Department",
                        id.ToString(),
                        "Name",
                        existingDept.Name,
                        department.Name,
                        $"Department name changed by {User.Identity.Name}"
                    );
                }

                if (existingDept.Description != department.Description)
                {
                    await _auditService.LogAsync(
                        "Department Updated",
                        "Department",
                        id.ToString(),
                        "Description",
                        existingDept.Description ?? "",
                        department.Description ?? "",
                        $"Department description changed by {User.Identity.Name}"
                    );
                }

                existingDept.Name = department.Name;
                existingDept.Description = department.Description;
                existingDept.IsActive = department.IsActive;
                existingDept.LastModifiedDate = DateTime.UtcNow;
                existingDept.LastModifiedBy = User.Identity.Name;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Department '{department.Name}' updated successfully.";
                return RedirectToAction(nameof(Index));
            }

            return View(department);
        }

        // POST: Department/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var department = await _context.Departments.FindAsync(id);
            if (department == null)
            {
                TempData["ErrorMessage"] = "Department not found.";
                return RedirectToAction(nameof(Index));
            }

            // Check if any users have this department
            var usersInDept = await _context.Users
                .Where(u => u.Department == department.Name)
                .CountAsync();

            if (usersInDept > 0)
            {
                TempData["ErrorMessage"] = $"Cannot delete department '{department.Name}' because {usersInDept} user(s) are assigned to it.";
                return RedirectToAction(nameof(Index));
            }

            _context.Departments.Remove(department);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                "Department Deleted",
                "Department",
                id.ToString(),
                null,
                department.Name,
                null,
                $"Department '{department.Name}' deleted by {User.Identity.Name}"
            );

            TempData["SuccessMessage"] = $"Department '{department.Name}' deleted successfully.";
            return RedirectToAction(nameof(Index));
        }
    }

    public class DepartmentViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
        public int UserCount { get; set; }
    }
}