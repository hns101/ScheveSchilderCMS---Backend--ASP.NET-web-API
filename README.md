# Backend API for De Scheve Schilder CMS

This is the backend for the De Scheve Schilder CMS, a full-stack application built for managing students and automating invoice generation. The API is built with ASP.NET Core and uses MongoDB as its primary database.

## 🚀 Key Technologies

* **Framework:** ASP.NET Core 9.0

* **Language:** C#

* **Database:** MongoDB

* **Real-time Communication:** N/A (Planned for future updates)

* **File Management:** Built-in file system handling

* **PDF Generation:** QuestPDF

* **Containerization:** Docker & Docker Compose

## 📦 Project Structure

The project follows a clean, layered architecture to separate concerns:

* **`Controllers/`**: Contains the API controllers that handle HTTP requests and responses. Each controller is responsible for a specific resource (e.g., `StudentsController.cs`).

* **`Services/`**: The core business logic layer. Services contain the methods for interacting with the database, generating PDFs, and managing files.

* **`Models/`**: Defines the data models (`Student.cs`, `Invoice.cs`) that represent your data in both the C# code and the MongoDB database.

* **`WebApplicationScheveCMS.csproj`**: The project file that defines dependencies (NuGet packages) and build settings.

* **`Dockerfile`**: A multi-stage Dockerfile to build and publish a lightweight production image of the API.

## ⚙️ Development Environment Setup

This project uses Docker Compose to run all services (backend, frontend, and database) in isolated containers.

### **Prerequisites**

* **.NET 9.0 SDK**: Required for building and running the application locally.

* **Docker Desktop**: Required to run the containers.

### **Configuration**

* **`appsettings.json`**: This file contains the primary configuration for the application. The MongoDB connection string and collection names are defined here.

* **CORS**: The application is configured to allow cross-origin requests from `http://localhost` (the Dockerized frontend) and `http://localhost:5173` (your local `npm run dev` server).

## 🚀 API Endpoints

The API is designed with RESTful principles. You can interact with these endpoints using tools like Postman or the built-in Swagger UI.

### **Swagger UI**

When the backend is running, you can view the complete API documentation at `http://localhost:5000/swagger`.

### **Students API (`/api/students`)**

| Method | Endpoint | Description | 
| ----- | ----- | ----- | 
| **`GET`** | `/api/students` | Retrieves a list of all students. | 
| **`GET`** | `/api/students/{id}` | Retrieves a single student's details, including their associated invoices. | 
| **`POST`** | `/api/students` | Creates a new student. Returns the newly created student object. | 
| **`PUT`** | `/api/students/{id}` | Updates an existing student. | 
| **`DELETE`** | `/api/students/{id}` | Deletes a student and their registration document. | 
| **`POST`** | `/api/students/{id}/registration-document` | Uploads a new registration document for a student. | 
| **`GET`** | `/api/students/{id}/registration-document` | Retrieves a student's registration document as a PDF file. | 
| **`DELETE`** | `/api/students/{id}/registration-document` | Deletes a student's registration document. | 

### **Invoices API (`/api/invoices`)**

| Method | Endpoint | Description | 
| ----- | ----- | ----- | 
| **`GET`** | `/api/invoices` | Retrieves a list of all invoices. | 
| **`GET`** | `/api/invoices/{id}` | Retrieves a single invoice's details. | 
| **`POST`** | `/api/invoices/batch-generate` | Creates a batch of invoices for multiple students. | 
| **`GET`** | `/api/invoices/file/{id}` | Retrieves an invoice's PDF file. | 
| **`DELETE`** | `/api/invoices/{id}` | Deletes an invoice and its PDF file. | 

### **Troubleshooting**

* **`500 Internal Server Error`**: Check the Docker logs for the `backend` service. A common cause is a `FormatException` from the MongoDB C# driver.

* **`Connection Refused`**: Ensure all Docker containers are running and that the ports are correctly mapped in your `docker-compose.yml` file.
