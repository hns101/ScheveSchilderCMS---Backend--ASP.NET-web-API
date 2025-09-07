using Microsoft.AspNetCore.Mvc;
using WebApplicationScheveCMS.Models;
using WebApplicationScheveCMS.Services;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Bson;

namespace WebApplicationScheveCMS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StudentsController : ControllerBase
    {
        private readonly StudentService _studentService;
        private readonly InvoiceService _invoiceService;
        private readonly IFileService _fileService;
        private readonly ILogger<StudentsController> _logger;
        private readonly IWebHostEnvironment _env;

        public StudentsController(
            StudentService studentService, 
            InvoiceService invoiceService, 
            IFileService fileService,
            ILogger<StudentsController> logger, 
            IWebHostEnvironment env)
        {
            _studentService = studentService;
            _invoiceService = invoiceService;
            _fileService = fileService;
            _logger = logger;
            _env = env;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<Student>>>> Get()
        {
            try
            {
                var students = await _studentService.GetAllAsync();
                _logger.LogInformation("Retrieved {Count} students", students.Count);
                
                return Ok(ApiResponse<List<Student>>.SuccessResult(students, $"Retrieved {students.Count} students"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all students");
                return StatusCode(500, ApiResponse<List<Student>>.ErrorResult("Internal server error when fetching all students"));
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<Student>>> Get(string id)
        {
            try
            {
                if (!IsValidObjectId(id))
                {
                    return BadRequest(ApiResponse<Student>.ErrorResult("Invalid student ID format"));
                }

                // Use the simple GetAsync method that works
                var student = await _studentService.GetAsync(id);

                if (student is null)
                {
                    _logger.LogWarning("Student with ID '{Id}' not found", id);
                    return NotFound(ApiResponse<Student>.ErrorResult($"Student with ID '{id}' not found"));
                }

                // Manually get invoices for this student
                try
                {
                    var invoices = await _invoiceService.GetInvoicesByStudentIdAsync(id);
                    student.Invoices = invoices;
                    _logger.LogInformation("Loaded {InvoiceCount} invoices for student {StudentId}", invoices.Count, id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not load invoices for student {StudentId}", id);
                    student.Invoices = new List<Invoice>(); // Empty list if invoices can't be loaded
                }

                return Ok(ApiResponse<Student>.SuccessResult(student));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting student with ID: {Id}", id);
                return StatusCode(500, ApiResponse<Student>.ErrorResult("Internal server error"));
            }
        }
        
        [HttpGet("byNumber/{studentNumber}")]
        public async Task<ActionResult<ApiResponse<Student>>> GetByStudentNumber(string studentNumber)
        {
            try
            {
                var student = await _studentService.GetByStudentNumberAsync(studentNumber);

                if (student is null)
                {
                    _logger.LogWarning("Student with student number '{StudentNumber}' not found", studentNumber);
                    return NotFound(ApiResponse<Student>.ErrorResult($"Student with student number '{studentNumber}' not found"));
                }

                // Manually get invoices for this student
                try
                {
                    var invoices = await _invoiceService.GetInvoicesByStudentIdAsync(student.Id!);
                    student.Invoices = invoices;
                    _logger.LogInformation("Loaded {InvoiceCount} invoices for student number {StudentNumber}", invoices.Count, studentNumber);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not load invoices for student number {StudentNumber}", studentNumber);
                    student.Invoices = new List<Invoice>(); // Empty list if invoices can't be loaded
                }

                return Ok(ApiResponse<Student>.SuccessResult(student));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting student with StudentNumber: '{StudentNumber}'", studentNumber);
                return StatusCode(500, ApiResponse<Student>.ErrorResult($"Internal server error when fetching student number {studentNumber}"));
            }
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<Student>>> Post(Student newStudent)
        {
            try
            {
                await _studentService.CreateAsync(newStudent);
                _logger.LogInformation("Created new student: {StudentName} ({StudentNumber})", newStudent.Name, newStudent.StudentNumber);
                
                return CreatedAtAction(nameof(Get), new { id = newStudent.Id }, 
                    ApiResponse<Student>.SuccessResult(newStudent, "Student created successfully"));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ApiResponse<Student>.ErrorResult(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating new student");
                return StatusCode(500, ApiResponse<Student>.ErrorResult($"Internal server error: {ex.Message}"));
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse<object>>> Update(string id, Student updatedStudent)
        {
            try
            {
                if (!IsValidObjectId(id))
                {
                    return BadRequest(ApiResponse<object>.ErrorResult("Invalid student ID format"));
                }

                var student = await _studentService.GetAsync(id);

                if (student is null)
                {
                    return NotFound(ApiResponse<object>.ErrorResult($"Student with ID '{id}' not found"));
                }

                updatedStudent.Id = student.Id;
                await _studentService.UpdateAsync(id, updatedStudent);
                
                _logger.LogInformation("Updated student: {StudentId}", id);

                return Ok(ApiResponse<object>.SuccessResult(null, "Student updated successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating student with ID: {Id}", id);
                return StatusCode(500, ApiResponse<object>.ErrorResult($"Internal server error when updating student ID {id}"));
            }
        }
        
        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse<object>>> Delete(string id)
        {
            try
            {
                if (!IsValidObjectId(id))
                {
                    return BadRequest(ApiResponse<object>.ErrorResult("Invalid student ID format"));
                }

                var student = await _studentService.GetAsync(id);

                if (student is null)
                {
                    return NotFound(ApiResponse<object>.ErrorResult($"Student with ID '{id}' not found"));
                }

                // Clean up associated files
                if (!string.IsNullOrEmpty(student.RegistrationDocumentPath))
                {
                    try
                    {
                        _fileService.DeleteFile(student.RegistrationDocumentPath);
                    }
                    catch (Exception fileEx)
                    {
                        _logger.LogWarning(fileEx, "Could not delete registration document: {FilePath}", student.RegistrationDocumentPath);
                    }
                }

                // Delete associated invoices
                try
                {
                    var invoices = await _invoiceService.GetInvoicesByStudentIdAsync(id);
                    foreach (var invoice in invoices)
                    {
                        if (!string.IsNullOrEmpty(invoice.InvoicePdfPath))
                        {
                            try
                            {
                                _fileService.DeleteFile(invoice.InvoicePdfPath);
                            }
                            catch (Exception fileEx)
                            {
                                _logger.LogWarning(fileEx, "Could not delete invoice PDF: {FilePath}", invoice.InvoicePdfPath);
                            }
                        }
                        await _invoiceService.RemoveAsync(invoice.Id!);
                    }
                    _logger.LogInformation("Deleted {InvoiceCount} invoices for student {StudentId}", invoices.Count, id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not delete invoices for student {StudentId}", id);
                }

                await _studentService.RemoveAsync(id);
                _logger.LogInformation("Deleted student: {StudentId}", id);
                
                return Ok(ApiResponse<object>.SuccessResult(null, "Student deleted successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting student with ID: {Id}", id);
                return StatusCode(500, ApiResponse<object>.ErrorResult($"Internal server error when deleting student ID {id}"));
            }
        }

        [HttpPost("{id}/registration-document")]
        public async Task<ActionResult<ApiResponse<object>>> UploadRegistrationDocument(string id, IFormFile file)
        {
            try
            {
                if (!IsValidObjectId(id))
                {
                    return BadRequest(ApiResponse<object>.ErrorResult("Invalid student ID format"));
                }

                var student = await _studentService.GetAsync(id);

                if (student is null)
                {
                    return NotFound(ApiResponse<object>.ErrorResult($"Student with ID '{id}' not found"));
                }

                if (file == null || file.Length == 0)
                {
                    return BadRequest(ApiResponse<object>.ErrorResult("No file uploaded"));
                }

                // Validate file type (should be PDF)
                if (!file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) && 
                    !Path.GetExtension(file.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(ApiResponse<object>.ErrorResult("Only PDF files are allowed for registration documents"));
                }

                // Delete old document if exists
                if (!string.IsNullOrEmpty(student.RegistrationDocumentPath))
                {
                    try
                    {
                        _fileService.DeleteFile(student.RegistrationDocumentPath);
                    }
                    catch (Exception fileEx)
                    {
                        _logger.LogWarning(fileEx, "Could not delete old registration document: {FilePath}", student.RegistrationDocumentPath);
                    }
                }

                var filePath = _fileService.SaveStudentDocument(id, file);

                student.RegistrationDocumentPath = filePath;
                await _studentService.UpdateAsync(id, student);
                
                _logger.LogInformation("Uploaded registration document for student {StudentId}: {FilePath}", id, filePath);

                return Ok(ApiResponse<object>.SuccessResult(new { filePath }, "Registration document uploaded successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading registration document for student with ID: {Id}", id);
                return StatusCode(500, ApiResponse<object>.ErrorResult("Internal server error during file upload"));
            }
        }
        
        [HttpDelete("{id}/registration-document")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteRegistrationDocument(string id)
        {
            try
            {
                if (!IsValidObjectId(id))
                {
                    return BadRequest(ApiResponse<object>.ErrorResult("Invalid student ID format"));
                }

                var student = await _studentService.GetAsync(id);

                if (student is null || string.IsNullOrEmpty(student.RegistrationDocumentPath))
                {
                    return NotFound(ApiResponse<object>.ErrorResult("Registration document not found"));
                }
                
                _fileService.DeleteFile(student.RegistrationDocumentPath);

                student.RegistrationDocumentPath = null;
                await _studentService.UpdateAsync(id, student);
                
                _logger.LogInformation("Deleted registration document for student {StudentId}", id);

                return Ok(ApiResponse<object>.SuccessResult(null, "Registration document deleted successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting registration document for student with ID: {Id}", id);
                return StatusCode(500, ApiResponse<object>.ErrorResult("Internal server error during file deletion"));
            }
        }

        [HttpGet("{id}/registration-document")]
        public async Task<IActionResult> GetRegistrationDocument(string id)
        {
            try
            {
                if (!IsValidObjectId(id))
                {
                    return BadRequest("Invalid student ID format");
                }

                var student = await _studentService.GetAsync(id);

                if (student == null || string.IsNullOrEmpty(student.RegistrationDocumentPath))
                {
                    return NotFound("Registration document not found");
                }

                var filePath = student.RegistrationDocumentPath;
                
                if (!_fileService.FileExists(filePath))
                {
                    return NotFound("Registration document file not found on server");
                }

                var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
                var fileName = Path.GetFileName(filePath);
                
                return File(fileBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting registration document for student ID {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // Helper method to validate ObjectId
        private static bool IsValidObjectId(string id)
        {
            return !string.IsNullOrEmpty(id) && 
                   id.Length == 24 && 
                   ObjectId.TryParse(id, out _);
        }
    }
}