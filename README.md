```markdown
# 💬 Enterprise Chat - Real-Time Messaging Platform

<div align="center">

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![SignalR](https://img.shields.io/badge/SignalR-Real--Time-FF6A00?style=for-the-badge&logo=signalr&logoColor=white)
![Blazor](https://img.shields.io/badge/Blazor-WASM-512BD4?style=for-the-badge&logo=blazor&logoColor=white)
![Redis](https://img.shields.io/badge/Redis-Caching-DC382D?style=for-the-badge&logo=redis&logoColor=white)
![SQL Server](https://img.shields.io/badge/SQL-Server-CC2927?style=for-the-badge&logo=microsoft-sql-server&logoColor=white)

[![GitHub Stars](https://img.shields.io/github/stars/AhmedFarouk04/SignalRChat?style=social)](https://github.com/AhmedFarouk04/SignalRChat/stargazers)
[![GitHub Forks](https://img.shields.io/github/forks/AhmedFarouk04/SignalRChat?style=social)](https://github.com/AhmedFarouk04/SignalRChat/network/members)
[![GitHub Issues](https://img.shields.io/github/issues/AhmedFarouk04/SignalRChat)](https://github.com/AhmedFarouk04/SignalRChat/issues)
[![MIT License](https://img.shields.io/badge/License-MIT-green.svg)](https://choosealicense.com/licenses/mit/)

### 🚀 Enterprise-Grade Real-Time Chat Application Built with Clean Architecture

[🎥 Watch Demo Video](https://youtu.be/your-video-link) | [🐛 Report Bug](https://github.com/AhmedFarouk04/SignalRChat/issues) | [✨ Request Feature](https://github.com/AhmedFarouk04/SignalRChat/issues)

![App Screenshot](https://via.placeholder.com/800x400?text=Enterprise+Chat+Screenshot)

</div>

---

## ✨ Features

### 🎯 Core Features

| Feature                 | Description                                       |
| ----------------------- | ------------------------------------------------- |
| **Real-Time Messaging** | Instant message delivery using SignalR WebSockets |
| **Typing Indicators**   | See when users are typing with Redis caching      |
| **Read Receipts**       | ✓ Sent, ✓ Delivered, ✓ Read status                |
| **Online Presence**     | Real-time online/offline status with "last seen"  |
| **Message Reactions**   | 👍 ❤️ 😂 😮 😢 😡 reactions to messages           |
| **Reply to Messages**   | Threaded conversations with message context       |
| **Forward Messages**    | Share messages across conversations               |
| **Edit/Delete**         | Edit or delete sent messages                      |

### 👥 Group Management

| Feature                | Description                                |
| ---------------------- | ------------------------------------------ |
| **Create Groups**      | Public/private groups with custom names    |
| **Add/Remove Members** | Full member management                     |
| **Admin Roles**        | Promote/demote group admins                |
| **Transfer Ownership** | Transfer group ownership to another member |
| **Leave/Delete**       | Leave groups or delete (for owners)        |

### 🔐 Security & Privacy

| Feature                | Description                       |
| ---------------------- | --------------------------------- |
| **JWT Authentication** | Secure token-based authentication |
| **Email Verification** | OTP verification via email        |
| **Password Hashing**   | PBKDF2 hashing for passwords      |
| **Block Users**        | Block unwanted users              |
| **Mute Conversations** | Mute noisy chats                  |
| **Rate Limiting**      | API protection against abuse      |

### 🚀 Advanced Features

| Feature                 | Description                            |
| ----------------------- | -------------------------------------- |
| **Pin Messages**        | Pin important messages with expiration |
| **Message Search**      | Full-text search across conversations  |
| **File Attachments**    | Upload and share files                 |
| **Email Notifications** | SMTP integration (Gmail)               |
| **Redis Caching**       | High-performance caching for presence  |
| **Clean Architecture**  | DDD, CQRS, Repository Pattern          |

---

## 🏗️ Architecture
```

┌─────────────────────────────────────────────────────────────┐
│ EnterpriseChat.Client │
│ (Blazor WebAssembly) │
└───────────────────────────────┬─────────────────────────────┘
│ SignalR
┌───────────────────────────────▼─────────────────────────────┐
│ EnterpriseChat.API │
│ (ASP.NET Core - Controllers/Hubs) │
└───────────────────────────────┬─────────────────────────────┘
│
┌───────────────────────────────▼─────────────────────────────┐
│ EnterpriseChat.Application │
│ (CQRS - MediatR - DTOs - Interfaces) │
└───────────────────────────────┬─────────────────────────────┘
│
┌───────────────────────────────▼─────────────────────────────┐
│ EnterpriseChat.Domain │
│ (Entities - ValueObjects - Enums) │
└───────────────────────────────┬─────────────────────────────┘
│
┌───────────────────────────────▼─────────────────────────────┐
│ EnterpriseChat.Infrastructure │
│ (Persistence - Repositories - Redis - EF Core) │
└─────────────────────────────────────────────────────────────┘

````

### 📐 Design Patterns Used

| Pattern | Implementation |
|---------|----------------|
| **Clean Architecture** | 5-layer separation of concerns |
| **CQRS** | Command-Query separation with MediatR |
| **Repository Pattern** | Data access abstraction |
| **Unit of Work** | Transaction management |
| **Domain Events** | Event-driven communication |
| **Options Pattern** | Strongly-typed configuration |
| **Dependency Injection** | Built-in DI container |

---

## 🛠️ Tech Stack

### Backend
| Technology | Purpose |
|------------|---------|
| **.NET 8** | Core framework |
| **ASP.NET Core** | Web API & SignalR Hubs |
| **SignalR** | Real-time communication |
| **Entity Framework Core 8** | ORM for SQL Server |
| **Redis** | Caching & Presence tracking |
| **MediatR** | CQRS implementation |
| **FluentValidation** | Request validation |
| **AutoMapper** | Object mapping |
| **Serilog** | Structured logging |
| **JWT** | Authentication |

### Frontend
| Technology | Purpose |
|------------|---------|
| **Blazor WebAssembly** | SPA framework |
| **SignalR Client** | Real-time client |
| **Local Storage** | Token persistence |
| **Custom CSS** | Responsive design |

### Database
| Technology | Purpose |
|------------|---------|
| **SQL Server** | Primary database |
| **Redis** | Distributed cache |
| **EF Core Migrations** | Database versioning |

### DevOps & Tools
| Tool | Purpose |
|------|---------|
| **Git** | Version control |
| **GitHub Actions** | CI/CD (planned) |
| **Docker** | Containerization (planned) |
| **User Secrets** | Development secrets |
| **Environment Variables** | Production configuration |

---

## 📦 Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) (LocalDB, Express, or Developer)
- [Redis](https://redis.io/download) (Windows: use Memurai or WSL)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) (or VS Code)
- [Node.js](https://nodejs.org/) (for client assets)

---

## 🚀 Getting Started

### 1. Clone the Repository
```bash
git clone https://github.com/AhmedFarouk04/SignalRChat.git
cd SignalRChat
````

### 2. Setup Database

```bash
# Update connection string in appsettings.json
cd EnterpriseChat.API

# Apply migrations
dotnet ef database update
```

### 3. Setup Redis

**Windows (using Memurai):**

```bash
# Download and install Memurai from https://www.memurai.com/
# It runs automatically as a service
```

**Linux/WSL:**

```bash
sudo apt-get install redis-server
sudo service redis-server start
```

**Docker:**

```bash
docker run --name redis -p 6379:6379 -d redis
```

### 4. Configure Email (Optional)

Add to User Secrets:

```bash
dotnet user-secrets init
dotnet user-secrets set "Email:Smtp:Username" "your-email@gmail.com"
dotnet user-secrets set "Email:Smtp:Password" "your-app-password"
dotnet user-secrets set "Jwt:Key" "your-secret-key-min-32-chars"
```

### 5. Run the Application

```bash
# Backend API
cd EnterpriseChat.API
dotnet run

# Frontend Client (in another terminal)
cd EnterpriseChat.Client
dotnet run
```

Open your browser and navigate to:

- API: `https://localhost:7000`
- Client: `https://localhost:5001`

---

## 📁 Project Structure

```
EnterpriseChat/
├── 📂 EnterpriseChat.API/              # Presentation Layer
│   ├── Controllers/                     # API endpoints
│   ├── Hubs/                            # SignalR hubs
│   ├── Middleware/                       # Exception handling
│   ├── Auth/                             # JWT & authentication
│   └── Program.cs                        # Startup configuration
│
├── 📂 EnterpriseChat.Application/       # Application Layer
│   ├── Features/                         # CQRS commands/queries
│   ├── DTOs/                             # Data transfer objects
│   ├── Interfaces/                        # Abstractions
│   └── Services/                          # Business logic
│
├── 📂 EnterpriseChat.Domain/            # Domain Layer
│   ├── Entities/                          # Domain models
│   ├── ValueObjects/                       # Value objects
│   ├── Enums/                              # Enumerations
│   └── Events/                             # Domain events
│
├── 📂 EnterpriseChat.Infrastructure/    # Infrastructure Layer
│   ├── Persistence/                        # DbContext & configs
│   ├── Repositories/                        # Data access
│   ├── Migrations/                          # EF Core migrations
│   └── Services/                             # External services
│
└── 📂 EnterpriseChat.Client/            # Blazor WebAssembly Client
    ├── Components/                          # Reusable UI
    ├── Pages/                                # Application pages
    ├── Services/                              # Client services
    └── wwwroot/                                # Static assets
```

---

## 🔑 Environment Variables

| Variable                               | Description           | Default                                  |
| -------------------------------------- | --------------------- | ---------------------------------------- |
| `ConnectionStrings__DefaultConnection` | SQL Server connection | `Server=.;Database=EnterpriseChatDb;...` |
| `ConnectionStrings__Redis`             | Redis connection      | `localhost:6379`                         |
| `Jwt__Key`                             | JWT signing key       | `YourSecretKeyHere`                      |
| `Jwt__Issuer`                          | JWT issuer            | `EnterpriseChat`                         |
| `Jwt__Audience`                        | JWT audience          | `EnterpriseChat.Clients`                 |
| `Email__Smtp__Username`                | SMTP username         | `your-email@gmail.com`                   |
| `Email__Smtp__Password`                | SMTP app password     | `your-app-password`                      |

---

## 📊 Database Schema

![Database Schema](https://via.placeholder.com/800x600?text=Database+Schema+Diagram)

### Key Tables

- **Users** - Application users
- **ChatRooms** - Individual and group chats
- **Messages** - Chat messages
- **MessageReceipts** - Read/delivery status
- **Reactions** - Message reactions
- **Attachments** - File attachments
- **BlockedUsers** - Blocked relationships
- **MutedRooms** - Muted conversations

---

## 🧪 Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

---

## 📈 Performance Optimizations

- ✅ **Response Caching** for frequent queries
- ✅ **Redis Distributed Cache** for presence data
- ✅ **Database Indexes** on frequently queried columns
- ✅ **Lazy Loading** with proxies
- ✅ **Connection Pooling** for database
- ✅ **Async/Await** throughout
- ✅ **Pagination** for message history
- ✅ **Compression** for SignalR messages

---

## 🔮 Roadmap

- [x] ✅ Real-time messaging with SignalR
- [x] ✅ JWT authentication
- [x] ✅ Email verification
- [x] ✅ Groups & admins
- [x] ✅ Reactions & replies
- [ ] 📱 Mobile app (MAUI)
- [ ] 🎥 Voice/Video calls
- [ ] 🔒 End-to-end encryption
- [ ] 🌐 Docker support
- [ ] ☸️ Kubernetes deployment
- [ ] 🔍 Elasticsearch integration
- [ ] 📊 Admin dashboard

---

## 🤝 Contributing

Contributions are welcome! Please follow these steps:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

### Contribution Guidelines

- Follow Clean Architecture principles
- Write unit tests for new features
- Update documentation
- Use conventional commits

---

## 📝 License

Distributed under the MIT License. See `LICENSE` for more information.

---

## 📧 Contact

**Ahmed Farouk**

[![GitHub](https://img.shields.io/badge/GitHub-AhmedFarouk04-181717?style=for-the-badge&logo=github)](https://github.com/AhmedFarouk04)
[![LinkedIn](https://img.shields.io/badge/LinkedIn-Connect-0A66C2?style=for-the-badge&logo=linkedin)](https://linkedin.com/in/ahmed-farouk-04)
[![Gmail](https://img.shields.io/badge/Email-Contact-EA4335?style=for-the-badge&logo=gmail)](mailto:af7974943@gmail.com)

Project Link: [https://github.com/AhmedFarouk04/SignalRChat](https://github.com/AhmedFarouk04/SignalRChat)

---

## ⭐ Support

If you like this project, please give it a star on GitHub! It helps others discover it.

<div align="center">

### Built with ❤️ using .NET 8

[![Star on GitHub](https://img.shields.io/github/stars/AhmedFarouk04/SignalRChat?style=social)](https://github.com/AhmedFarouk04/SignalRChat/stargazers)

</div>
```
