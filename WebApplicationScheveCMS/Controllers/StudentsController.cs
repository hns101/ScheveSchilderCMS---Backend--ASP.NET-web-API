using Microsoft.AspNetCore.Mvc;
using WebApplicationScheveCMS.Models; // Use the single Student model
using WebApplicationScheveCMS.Services;

namespace WebApplicationScheveCMS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StudentsController : ControllerBase
    {
        private readonly StudentService _studentService;
        private readonly InvoiceService _invoiceService; // Keep this if needed for other logic later

        public StudentsController(StudentService studentService, InvoiceService invoiceService)
        {
            _studentService = studentService;
            _invoiceService = invoiceService;
        }

        // GET: api/students
        [HttpGet]
        public async Task<ActionResult<List<Student>>> Get() =>
            await _studentService.GetAllAsync();

        // GET: api/students/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Student>> Get(string id)
        {
            var student = await _studentService.GetStudentWithInvoicesAsync(id);

            if (student is null)
            {
                return NotFound();
            }

            return student;
        }

        // POST: api/students
        [HttpPost]
        public async Task<IActionResult> Post(Student newStudent)
        {
            try
            {
                await _studentService.CreateAsync(newStudent);
                return CreatedAtAction(nameof(Get), new { id = newStudent.Id }, newStudent);
            }
            catch (InvalidOperationException ex)
            {
                // Return a 409 Conflict if student number already exists
                return Conflict(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // PUT: api/students/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, Student updatedStudent)
        {
            var student = await _studentService.GetAsync(id);

            if (student is null)
            {
                return NotFound();
            }

            updatedStudent.Id = student.Id; // Ensure the ID from the URL is used
            await _studentService.UpdateAsync(id, updatedStudent);

            return NoContent();
        }

        // DELETE: api/students/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var student = await _studentService.GetAsync(id);

            if (student is null)
            {
                return NotFound();
            }

            await _studentService.RemoveAsync(id);

            return NoContent();
        }
    }
}
