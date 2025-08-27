using Microsoft.AspNetCore.Mvc;
using WebApplicationScheveCMS.Models;
using WebApplicationScheveCMS.Services;

namespace WebApplicationScheveCMS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StudentsController : ControllerBase
    {
        private readonly StudentService _studentService;
        private readonly InvoiceService _invoiceService;

        public StudentsController(StudentService studentService, InvoiceService invoiceService)
        {
            _studentService = studentService;
            _invoiceService = invoiceService;
        }

        // GET: api/students
        [HttpGet]
        public async Task<ActionResult<List<Student>>> Get() =>
            await _studentService.GetAsync();

        // GET: api/students/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Student>> Get(string id)
        {
            var student = await _studentService.GetAsync(id);

            if (student is null)
            {
                return NotFound();
            }

            // You can add logic here to get the student's invoices as well
            // student.Invoices = await _invoiceService.GetInvoicesByStudentIdAsync(id);

            return student;
        }

        // POST: api/students
        [HttpPost]
        public async Task<IActionResult> Post(Student newStudent)
        {
            await _studentService.CreateAsync(newStudent);
            return CreatedAtAction(nameof(Get), new { id = newStudent.Id }, newStudent);
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

            updatedStudent.Id = student.Id;
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