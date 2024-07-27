using CsvDataManager.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http.Extensions;
using CsvDataManager.Extensions;

namespace CsvDataManager.Controllers
{
    public class PersonController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const string ImportedPersonsSessionKey = "ImportedPersons"; // Session key for imported persons

        public PersonController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Person/Index
        public async Task<IActionResult> Index()
        {
            return View(await _context.Persons.ToListAsync());
        }

        // GET: Person/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Person/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FirstName,Surname,Age,Sex,Mobile,Active")] Person person)
        {
            if (ModelState.IsValid)
            {
                person.Identity = _context.Persons.Any() ? _context.Persons.Max(p => p.Identity) + 1 : 1;
                _context.Add(person);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(person);
        }

        // GET: Person/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var person = await _context.Persons.FindAsync(id);
            if (person == null)
            {
                return NotFound();
            }
            return View(person);
        }

        // POST: Person/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Identity,FirstName,Surname,Age,Sex,Mobile,Active")] Person person)
        {
            if (id != person.Identity)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(person);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PersonExists(person.Identity))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(person);
        }

        // GET: Person/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var person = await _context.Persons.FirstOrDefaultAsync(m => m.Identity == id);
            if (person == null)
            {
                return NotFound();
            }

            return View(person);
        }

        // POST: Person/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var person = await _context.Persons.FindAsync(id);
            _context.Persons.Remove(person);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: Person/Import
        public IActionResult Import()
        {
            // Retrieve imported records from session
            var importedRecords = HttpContext.Session.GetObjectFromJson<List<Person>>(ImportedPersonsSessionKey);
            return View(importedRecords ?? new List<Person>());
        }

        // POST: Person/Import
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Import(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["ErrorMessage"] = "Please select a CSV file to upload.";
                return RedirectToAction(nameof(Import));
            }

            try
            {
                using (var reader = new StreamReader(file.OpenReadStream()))
                {
                    var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
                    {
                        HasHeaderRecord = true,
                        IgnoreBlankLines = true
                    };

                    var csv = new CsvReader(reader, csvConfig);

                    // Reading records from the CSV file
                    var importedPersons = csv.GetRecords<Person>().ToList();

                    // Validate the records
                    if (importedPersons.Any())
                    {
                        TempData["SuccessMessage"] = "CSV file uploaded successfully!";

                        // Store the imported records in session
                        HttpContext.Session.SetObjectAsJson(ImportedPersonsSessionKey, importedPersons);

                        return RedirectToAction(nameof(Import));
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "No records found in the CSV file.";
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An error occurred while importing the file: {ex.Message}";
            }

            return RedirectToAction(nameof(Import));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveImported()
        {
            // Retrieve imported records from TempData
            var importedRecords = TempData["ImportedRecords"] as List<Person>;
            if (importedRecords == null || !importedRecords.Any())
            {
                TempData["ErrorMessage"] = "No imported data to save.";
                return RedirectToAction(nameof(Import));
            }

            try
            {
                // Use a HashSet to keep track of existing identities
                var existingPersons = await _context.Persons
                    .Where(p => importedRecords.Select(r => r.Identity).Contains(p.Identity))
                    .ToListAsync();

                var existingPersonIds = new HashSet<int>(existingPersons.Select(p => p.Identity));

                foreach (var person in importedRecords)
                {
                    if (existingPersonIds.Contains(person.Identity))
                    {
                        // Update the existing entity
                        var existingPerson = existingPersons.First(p => p.Identity == person.Identity);
                        _context.Entry(existingPerson).CurrentValues.SetValues(person);
                    }
                    else
                    {
                        // Add new entity
                        person.Identity = _context.Persons.Any() ? _context.Persons.Max(p => p.Identity) + 1 : 1;
                        _context.Persons.Add(person);
                    }
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Imported data saved successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error saving imported data: {ex.Message}";
                return RedirectToAction(nameof(Import));
            }
        }


        // GET: Person/ClearImported
        public IActionResult ClearImported()
        {
            HttpContext.Session.Remove(ImportedPersonsSessionKey);
            TempData["SuccessMessage"] = "Imported data cleared.";
            return RedirectToAction(nameof(Import));
        }

        private bool PersonExists(int id)
        {
            return _context.Persons.Any(e => e.Identity == id);
        }
    }
}
