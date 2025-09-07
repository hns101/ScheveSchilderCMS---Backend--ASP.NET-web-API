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

namespace WebApplicationScheveCMS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StudentsController : ControllerBase
    {
        private readonly StudentService _studentService;
        private readonly InvoiceService _invoiceService;
        private readonly IFileService _fileService; // Changed to interface
        private readonly ILogger<StudentsController> _logger;
        private readonly IWebHostEnvironment _env;

        public StudentsController(
            StudentService studentService, 
            InvoiceService invoiceService, 
            IFileService fileService, // Changed to interface
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
                var student = await _studentService.GetStudentWithInvoicesAsync(id);

                if (student is null)
                {
                    _logger.LogWarning("Student with ID '{Id}' not found", id);
                    return NotFound(ApiResponse<Student>.ErrorResult($"Student with ID '{id}' not found"));
                }

                return Ok(ApiResponse<Student>.SuccessResult(student));
            }
            catch (FormatException ex)
            {
                _logger.LogError(ex, "Invalid student ID format: '{Id}'", id);
                return BadRequest(ApiResponse<Student>.ErrorResult($"Invalid student ID format: {id}"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting student with ID: {Id}", id);
                return StatusCode(500, ApiResponse<Student>.ErrorResult($"Internal server error when fetching student ID {id}"));
            }
        }
        
        [HttpGet("byNumber/{studentNumber}")]
        public async Task<ActionResult<ApiResponse<Student>>> GetByStudentNumber(string studentNumber)
        {
            try
            {
                var student = await _studentService.GetStudentWithInvoicesByNumberAsync(studentNumber);

                if (student is null)
                {
                    _logger.LogWarning("Student with student number '{StudentNumber}' not found", studentNumber);
                    return NotFound(ApiResponse<Student>.ErrorResult($"Student with student number '{studentNumber}' not found"));
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
                var student = await _studentService.GetAsync(id);

                if (student is null)
                {
                    return NotFound(ApiResponse<object>.ErrorResult($"Student with ID '{id}' not found"));
                }

                updatedStudent.Id = student.Id;
                await _studentService.UpdateAsync(id, updatedStudent);

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
                var student = await _studentService.GetAsync(id);

                if (student is null)
                {
                    return NotFound(ApiResponse<object>.ErrorResult($"Student with ID '{id}' not found"));
                }

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

                await _studentService.RemoveAsync(id);
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
                var student = await _studentService.GetAsync(id);

                if (student is null)
                {
                    return NotFound(ApiResponse<object>.ErrorResult($"Student with ID '{id}' not found"));
                }

                if (file == null || file.Length == 0)
                {
                    return BadRequest(ApiResponse<object>.ErrorResult("No file uploaded"));
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
                var student = await _studentService.GetAsync(id);

                if (student is null || string.IsNullOrEmpty(student.RegistrationDocumentPath))
                {
                    return NotFound(ApiResponse<object>.ErrorResult("Registration document not found"));
                }
                
                _fileService.DeleteFile(student.RegistrationDocumentPath);

                student.RegistrationDocumentPath = null;
                await _studentService.UpdateAsync(id, student);

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
    }
}