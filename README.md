# Recam Real Estate Platform

A full-stack real estate listing management system built with **.NET 9**, **ASP.NET Core Web API**, and **Entity Framework Core**. The platform enables photography companies, agents, and admins to collaboratively manage property listings with media assets, workflows, and role-based access control.

---

## 🚀 Features

### Core Functionality
- **User Management**: Multi-role authentication system (Admin, Photography Company, Agent)
- **Listing Case Management**: Create, update, delete, and track property listings through workflow statuses
- **Media Asset Management**: Upload, organize, and download property images, videos, floor plans, and VR tours
- **Role-Based Access Control**: Fine-grained permissions using JWT authentication and ASP.NET Identity
- **Audit Logging**: Complete activity tracking using MongoDB for compliance and traceability
- **Email Notifications**: Automated email service for account creation and status updates

### Technical Highlights
- **Clean Architecture**: Organized into API, Services, Repositories, and Models layers
- **SOLID Principles**: Modular, maintainable, and testable codebase
- **RESTful API Design**: Comprehensive endpoints with Swagger documentation
- **Azure Blob Storage**: Secure cloud storage for media files with download/upload capabilities
- **Unit Testing**: Extensive test coverage using xUnit, Moq, and NUnit

---

## 🛠️ Tech Stack

**Backend**
- .NET 9
- ASP.NET Core Web API
- Entity Framework Core
- ASP.NET Identity (Authentication & Authorization)

**Database**
- SQL Server (Primary relational data)
- SQLite (In-memory testing)
- MongoDB (Activity logging and audit trails)

**Cloud & Storage**
- Azure Blob Storage (Media file management)

**Testing**
- xUnit
- Moq (Mocking framework)
- NUnit

**Tools**
- JetBrains Rider
- Swagger/OpenAPI
- Docker

---

## 📂 Project Structure

```
Recam/
├── Recam.Api/              # Web API controllers and endpoints
├── Recam.Services/         # Business logic layer
├── Recam.Repositories/     # Data access layer
├── Recam.Models/           # Entity models and DTOs
├── Recam.Common/           # Shared utilities and helpers
├── Recam.DataAccess/       # DbContext and configurations
└── Recam.UnitTests/        # Unit tests with xUnit and Moq
```

---

## 🔑 Key API Endpoints

### Authentication
- `POST /api/auth/login` - User login with JWT token generation
- `POST /api/auth/register` - Photography company registration
- `GET /api/users/me` - Get current authenticated user info

### Listing Management
- `GET /api/listings` - Retrieve all listing cases (with role-based filtering)
- `POST /api/listings` - Create new listing case
- `GET /api/listings/{id}` - Get listing details
- `PUT /api/listings/{id}` - Update listing information
- `DELETE /api/listings/{id}` - Soft delete listing (sets IsDeleted flag)
- `PATCH /api/listings/{id}/status` - Update listing workflow status

### Media Management
- `POST /api/listings/{id}/media` - Upload media assets (images, videos, floor plans, VR tours)
- `GET /api/listings/{id}/media` - Retrieve all media for a listing (grouped by type)
- `DELETE /api/media/{id}` - Delete media file
- `GET /api/media/{id}/download` - Download single media file
- `GET /api/listings/{id}/download` - Generate ZIP of all listing media
- `PUT /api/listings/{id}/selected-media` - Agent selects final media (max 10 images)
- `PUT /api/listings/{id}/cover-image` - Admin sets cover image

### Agent Management
- `GET /api/agents` - Get all agents (Admin only)
- `POST /api/agents` - Create agent account with auto-generated credentials
- `GET /api/agents/search?email={email}` - Search agent by exact email match
- `POST /api/photography-companies/{id}/agents` - Add agent to photography company

### Additional Features
- `POST /api/listings/{id}/contacts` - Add case contact information
- `GET /api/listings/{id}/contacts` - Get all contacts for a listing
- `POST /api/listings/{id}/publish` - Generate shareable public link
- `PUT /api/users/password` - Update user password

---

## 🧪 Testing

The project includes comprehensive unit tests covering:

- ✅ Listing case CRUD operations
- ✅ Media upload, retrieval, and deletion
- ✅ Agent selection and media management workflows
- ✅ Authentication and authorization logic
- ✅ Service layer business rules validation
- ✅ Repository pattern with mocked database contexts

**Run Tests:**
```bash
dotnet test
```

---

## 🏗️ Architecture Patterns

- **Repository Pattern**: Abstracts data access logic for testability
- **Service Layer**: Encapsulates business logic separate from API controllers
- **Dependency Injection**: ASP.NET Core built-in DI container
- **DTO Pattern**: Data Transfer Objects for clean API contracts
- **Unit of Work**: Manages transactions across multiple repositories

---

## 🔐 Security Features

- JWT token-based authentication
- Role-based authorization (Admin, PhotoCompany, Agent)
- Password hashing with ASP.NET Identity
- Secure file upload with type and size validation
- Audit logging for all critical operations

---

## 📊 Database Schema Highlights

**SQL Server Tables:**
- Users (ASP.NET Identity)
- ListingCases
- MediaAssets
- SelectedMedia
- CaseContacts
- PhotoCompanyAgents

**MongoDB Collections:**
- CaseHistory (Audit logs)
- MediaSelectionHistory
- UserActivityLogs

---

## 🚀 Getting Started

### Prerequisites
- .NET 9 SDK
- SQL Server
- MongoDB (optional, for logging)
- Azure Storage Account (for media uploads)

### Installation

1. **Clone the repository**
```bash
git clone https://github.com/JohnnySiuky/Recam-Realestate.git
cd Recam-Realestate
```

2. **Configure appsettings.json**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Your SQL Server connection string",
    "MongoDB": "Your MongoDB connection string"
  },
  "AzureBlobStorage": {
    "ConnectionString": "Your Azure Storage connection string",
    "ContainerName": "media-files"
  }
}
```

3. **Apply database migrations**
```bash
cd Recam.Api
dotnet ef database update
```

4. **Run the application**
```bash
dotnet run
```

5. **Access API Documentation**
Navigate to: `https://localhost:5001/swagger`

---

## 📈 Future Enhancements

- [ ] Real-time notifications with SignalR
- [ ] Advanced search with Elasticsearch
- [ ] Mobile app using React Native
- [ ] Property analytics dashboard
- [ ] Integration with external MLS systems
- [ ] Automated property valuation using ML

---

## 👨‍💻 Author

**Johnny Siu**  
- Email: siukwokyu@gmail.com
- LinkedIn: [www.linkedin.com/in/kwok-yu-siu-6185bb2b0](www.linkedin.com/in/kwok-yu-siu-6185bb2b0)
- GitHub: [@JohnnySiuky](https://github.com/JohnnySiuky)

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgments

- Built as a demonstration of modern .NET development practices
- Inspired by real-world property management platforms
- Thanks to the ASP.NET Core and Entity Framework communities
