using Microsoft.AspNetCore.Mvc;
using WebApplicationScheveCMS.Models;
using WebApplicationScheveCMS.Services;
using Microsoft.Extensions.Logging;
using System;
using System.IO;

namespace WebApplicationScheveCMS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StudentsController : ControllerBase
    {
        private readonly StudentService _studentService;
        private readonly InvoiceService _invoiceService;
        private readonly FileService _fileService;
        private readonly ILogger<StudentsController> _logger;

        public StudentsController(StudentService studentService, InvoiceService invoiceService, FileService fileService, ILogger<StudentsController> logger)
        {
            _studentService = studentService;
            _invoiceService = invoiceService;
            _fileService = fileService;
            _logger = logger;
        }

        // GET: api/students
        [HttpGet]
        public async Task<ActionResult<List<Student>>> Get()
        {
            try
            {
                return await _studentService.GetAllAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all students.");
                return StatusCode(500, "Internal server error when fetching all students.");
            }
        }

        // GET: api/students/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Student>> Get(string id)
        {
            try
            {
                var student = await _studentService.GetStudentWithInvoicesAsync(id);

                if (student is null)
                {
                    _logger.LogWarning($"Student with ID '{id}' not found.");
                    return NotFound();
                }

                return student;
            }
            catch (FormatException ex)
            {
                _logger.LogError(ex, $"Invalid student ID format: '{id}'.");
                return BadRequest($"Invalid student ID format: {id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting student with ID: {id}.");
                return StatusCode(500, $"Internal server error when fetching student ID {id}.");
            }
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
                return Conflict(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating new student.");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // PUT: api/students/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, Student updatedStudent)
        {
            try
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
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating student with ID: {id}.");
                return StatusCode(500, $"Internal server error when updating student ID {id}.");
            }
        }
        
        // DELETE: api/students/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                var student = await _studentService.GetAsync(id);

                if (student is null)
                {
                    return NotFound();
                }

                if (!string.IsNullOrEmpty(student.RegistrationDocumentPath))
                {
                    _fileService.DeleteFile(student.RegistrationDocumentPath);
                }

                await _studentService.RemoveAsync(id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting student with ID: {id}.");
                return StatusCode(500, $"Internal server error when deleting student ID {id}.");
            }
        }

        // New endpoint to handle file upload
        [HttpPost("{id}/registration-document")]
        public async Task<IActionResult> UploadRegistrationDocument(string id, IFormFile file)
        {
            try
            {
                var student = await _studentService.GetAsync(id);

                if (student is null)
                {
                    return NotFound();
                }

                // If a document already exists, delete it first
                if (!string.IsNullOrEmpty(student.RegistrationDocumentPath))
                {
                    _fileService.DeleteFile(student.RegistrationDocumentPath);
                }

                // Save the new file
                var filePath = _fileService.SaveStudentDocument(id, file);

                // Update the student record with the new file path
                student.RegistrationDocumentPath = filePath;
                await _studentService.UpdateAsync(id, student);

                return Ok(new { filePath });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error uploading registration document for student with ID: {id}.");
                return StatusCode(500, "Internal server error during file upload.");
            }
        }
        
        // New endpoint to delete a registration document
        [HttpDelete("{id}/registration-document")]
        public async Task<IActionResult> DeleteRegistrationDocument(string id)
        {
            try
            {
                var student = await _studentService.GetAsync(id);

                if (student is null || string.IsNullOrEmpty(student.RegistrationDocumentPath))
                {
                    return NotFound();
                }
                
                // Delete the file from the file system
                _fileService.DeleteFile(student.RegistrationDocumentPath);

                // Remove the file path from the student record
                student.RegistrationDocumentPath = null;
                await _studentService.UpdateAsync(id, student);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting registration document for student with ID: {id}.");
                return StatusCode(500, "Internal server error during file deletion.");
            }
        }
    }
}
