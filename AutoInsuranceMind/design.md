# Auto Insurance AI Self-Service Portal - Design Document

## 1. System Architecture

### High-Level Architecture
```
┌─────────────────────────────────────────────────────────────────┐
│                    Azure Cloud Infrastructure                   │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌──────────────────┐         ┌──────────────────────────────┐ │
│  │   React Frontend │         │    .NET Core Web API         │ │
│  │  (Static Web App)│◄────────┤   (App Service)              │ │
│  │                  │  HTTPS  │                              │ │
│  │ - Login          │         │ - Auth Controller            │ │
│  │ - Policies       │         │ - Policies Controller        │ │
│  │ - Upload         │         │ - Upload Controller          │ │
│  │ - Chat (RAG)     │         │ - AI Controller              │ │
│  └──────────────────┘         │ - Notification Service       │ │
│                               └──────────────────────────────┘ │
│                                        │                       │
│                    ┌───────────────────┼───────────────────┐   │
│                    │                   │                   │   │
│              ┌─────▼──────┐   ┌────────▼────────┐  ┌──────▼──┐ │
│              │   Azure     │   │  Azure Cognitive│  │ Azure   │ │
│              │   Blob      │   │     Search      │  │ OpenAI  │ │
│              │  Storage    │   │   (Vector DB)   │  │         │ │
│              │             │   │                 │  │ - Chat  │ │
│              │ - Policy    │   │ - RAG Index     │  │ - Embed │ │
│              │   Docs      │   │ - Vector Search │  │         │ │
│              └─────────────┘   └─────────────────┘  └────┬────┘ │
│                                                           │     │
│                                      ┌────────────────────┘     │
│                                      │                          │
│              ┌──────────────────┬────▼──────────────────────┐   │
│              │ Azure Document   │  Semantic Kernel          │   │
│              │ Intelligence     │  (AI Orchestration)       │   │
│              │                  │                           │   │
│              │ - Text Extract   │  - Memory Management      │   │
│              │ - Layout Analyze │  - Plugin Integration     │   │
│              │                  │  - Prompt Engineering     │   │
│              └──────────────────┴────┬──────────────────────┘   │
│                                      │                          │
│                              ┌───────▼────────┐                 │
│                              │  Azure Comm    │                 │
│                              │  Services      │                 │
│                              │                │                 │
│                              │ - Email Notify │                 │
│                              │ - SMS Notify   │                 │
│                              └────────────────┘                 │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

## 2. Data Flow Diagrams

### 2.1 Document Upload & RAG Indexing Flow
```
Customer Upload
     │
     ▼
┌──────────────────┐
│ Frontend          │
│ FileUpload.tsx    │
└────────┬─────────┘
         │ POST /api/upload
         ▼
┌──────────────────────────┐
│ Backend                  │
│ UploadController         │
└────────┬─────────────────┘
         │
    ┌────┴────┐
    │          │
    ▼          ▼
┌──────────┐ ┌──────────────────────┐
│  Blob    │ │ Document Service     │
│ Storage  │ │                      │
└──────────┘ │ 1. Validate file     │
             │ 2. Extract text      │
             │    (Doc Intelligence)│
             │ 3. Create embeddings │
             │    (OpenAI API)      │
             │ 4. Index in Cognitive
             │    Search            │
             └────────┬─────────────┘
                      │
                      ▼
             ┌──────────────────┐
             │ Cognitive Search │
             │ (Vector Index)   │
             └──────────────────┘

Status: Ready for RAG queries
```

### 2.2 AI Chatbot (RAG) Query Flow
```
Customer Query
     │
     ▼
┌──────────────────┐
│ Frontend         │
│ ChatBot.tsx      │
└────────┬─────────┘
         │ POST /api/ai/chat
         ▼
┌──────────────────────────┐
│ Backend                  │
│ AIController             │
└────────┬─────────────────┘
         │
         ▼
┌──────────────────────────┐
│ Semantic Kernel          │
│ AIService                │
└────────┬─────────────────┘
         │
    ┌────┴──────┐
    │            │
    ▼            ▼
┌──────────┐ ┌──────────────────────┐
│ Memory & │ │ RAG Pipeline:        │
│ History  │ │ 1. Embed query       │
└──────────┘ │    (OpenAI)          │
             │ 2. Search relevant   │
             │    docs (Cog Search) │
             │ 3. Format prompt     │
             │ 4. Call LLM (OpenAI) │
             │ 5. Stream response   │
             └────────┬─────────────┘
                      │
                      ▼
             ┌──────────────────┐
             │ API Response     │
             │ (with citations) │
             └────────┬─────────┘
                      │
                      ▼
             Frontend Display
```

### 2.3 Policy Update & Notification Flow
```
Customer Updates Coverage
          │
          ▼
┌────────────────────────┐
│ Frontend               │
│ PolicyDashboard.tsx    │
└────────┬───────────────┘
         │ PUT /api/policies/{id}/coverages/{covId}
         ▼
┌────────────────────────┐
│ Backend                │
│ PoliciesController     │
└────────┬───────────────┘
         │
    ┌────┴────┐
    │          │
    ▼          ▼
┌──────────┐ ┌──────────────────────┐
│ Mock DB  │ │ Notification Service │
│ Update   │ │                      │
└──────────┘ │ - Format email       │
             │ - Format SMS         │
             │ - Queue notification │
             └────────┬─────────────┘
                      │
                      ▼
             ┌──────────────────┐
             │ Azure Comm Svc   │
             │                  │
             ├─ Email           │
             └─ SMS             │
                      │
                      ▼
             Customer Notification
```

## 3. Component Architecture

### 3.1 Backend Components
```
API Layer
├── Controllers
│   ├── AuthController
│   │   ├── Login(credentials)
│   │   └── Logout()
│   │
│   ├── PoliciesController
│   │   ├── GetPolicies()
│   │   ├── GetPolicy(id)
│   │   ├── UpdatePolicy(id, data)
│   │   ├── GetCoverages(policyId)
│   │   └── UpdateCoverage(policyId, covId, data)
│   │
│   ├── UploadController
│   │   ├── UploadDocument(file)
│   │   ├── GetDocuments()
│   │   ├── GetDocument(id)
│   │   └── DeleteDocument(id)
│   │
│   └── AIController
│       ├── Chat(message)
│       ├── GetChatHistory()
│       └── ResetChat()
│
Service Layer
├── AIService (Semantic Kernel)
│   ├── InitializeKernel()
│   ├── ProcessMessage(message)
│   ├── RetrieveRAGContext()
│   └── GenerateResponse()
│
├── DocumentService
│   ├── ProcessUploadedFile(file)
│   ├── ExtractText(blob)
│   ├── CreateEmbeddings(text)
│   ├── IndexInCognitiveSearch()
│   └── GetDocumentMetadata()
│
├── NotificationService
│   ├── SendEmailNotification()
│   ├── SendSMSNotification()
│   └── QueueNotification()
│
├── AuthService
│   ├── ValidateCredentials()
│   ├── GenerateJWT()
│   ├── ValidateToken()
│   └── RefreshToken()
│
└── PolicyService
    ├── GetCustomerPolicies()
    ├── GetPolicyDetails()
    ├── UpdatePolicy()
    ├── GetCoverages()
    └── UpdateCoverage()

Data Layer
├── MockDataStore
│   ├── Customers[]
│   ├── Policies[]
│   ├── Coverages[]
│   └── UploadedDocuments[]
│
└── Azure Services
    ├── Blob Storage Client
    ├── Cognitive Search Client
    ├── OpenAI Client
    └── Document Intelligence Client
```

### 3.2 Frontend Components
```
src/
├── pages/
│   ├── Login.tsx
│   │   ├── useAuth() hook
│   │   ├── Form validation
│   │   └── Error handling
│   │
│   └── PolicyDashboard.tsx
│       ├── Layout (header, sidebar, main)
│       ├── usePolicy() hook
│       ├── useChat() hook
│       ├── useUpload() hook
│       └── Display management
│
├── components/
│   ├── PolicyCard.tsx
│   │   ├── Display policy info
│   │   ├── Edit coverage button
│   │   └── Status badge
│   │
│   ├── CoverageModal.tsx
│   │   ├── Edit form
│   │   ├── Validation
│   │   └── Submit handler
│   │
│   ├── FileUpload.tsx
│   │   ├── Drag-drop area
│   │   ├── File validation
│   │   ├── Progress indicator
│   │   └── Error display
│   │
│   ├── ChatBot.tsx
│   │   ├── Message list
│   │   ├── Input field
│   │   ├── Send handler
│   │   ├── Streaming response
│   │   └── Loading state
│   │
│   ├── ChatMessage.tsx
│   │   ├── User message
│   │   ├── AI response (with citations)
│   │   └── Timestamp
│   │
│   └── Navigation.tsx
│       ├── Navbar
│       ├── Sidebar menu
│       └── Logout button
│
├── hooks/
│   ├── useAuth.ts
│   ├── usePolicy.ts
│   ├── useChat.ts
│   ├── useUpload.ts
│   └── useFetch.ts
│
├── services/
│   ├── apiClient.ts
│   ├── authService.ts
│   ├── policyService.ts
│   ├── chatService.ts
│   └── uploadService.ts
│
├── types/
│   ├── auth.ts
│   ├── policy.ts
│   ├── chat.ts
│   └── upload.ts
│
├── styles/
│   ├── globals.css
│   ├── layout.module.css
│   └── components.module.css
│
├── App.tsx (Router setup)
└── index.tsx (Entry point)
```

## 4. Database Schema (Mock Data)

### Customers Table
```
{
  id: UUID
  name: string
  email: string
  phoneNumber: string
  createdAt: datetime
  updatedAt: datetime
}
```

### Policies Table
```
{
  id: UUID
  customerId: UUID (FK)
  policyNumber: string (unique)
  type: enum (auto, home, life)
  status: enum (active, expired, pending, cancelled)
  startDate: date
  endDate: date
  premium: decimal
  createdAt: datetime
  updatedAt: datetime
}
```

### Coverages Table
```
{
  id: UUID
  policyId: UUID (FK)
  type: enum (liability, collision, comprehensive, property, medical)
  limit: decimal
  deductible: decimal
  description: string
  createdAt: datetime
  updatedAt: datetime
}
```

### UploadedDocuments Table
```
{
  id: UUID
  customerId: UUID (FK)
  policyId: UUID (FK, nullable)
  fileName: string
  fileType: string
  blobUrl: string
  fileSize: long
  vectorIndexId: string (Cognitive Search)
  extractedText: text
  uploadedAt: datetime
  processedAt: datetime
  status: enum (uploading, processing, indexed, error)
}
```

### ChatHistory Table
```
{
  id: UUID
  customerId: UUID (FK)
  policyId: UUID (FK, nullable)
  uploadedDocId: UUID (FK, nullable)
  userMessage: text
  aiResponse: text
  ragContext: json (retrieved chunks)
  model: string
  tokensUsed: int
  createdAt: datetime
}
```

## 5. API Contract Design

### Authentication Endpoints

#### POST /api/auth/login
**Request:**
```json
{
  "email": "customer@example.com",
  "password": "password123"
}
```
**Response (200):**
```json
{
  "token": "jwt_token_here",
  "customer": {
    "id": "uuid",
    "name": "John Doe",
    "email": "customer@example.com"
  }
}
```

### Policy Endpoints

#### GET /api/policies
**Response (200):**
```json
{
  "policies": [
    {
      "id": "uuid",
      "policyNumber": "AUTO-2026-001",
      "type": "auto",
      "status": "active",
      "startDate": "2024-01-01",
      "endDate": "2025-01-01",
      "premium": 1200.00
    }
  ]
}
```

#### PUT /api/policies/{id}/coverages/{covId}
**Request:**
```json
{
  "type": "collision",
  "limit": 50000,
  "deductible": 500
}
```
**Response (200):**
```json
{
  "success": true,
  "message": "Coverage updated successfully",
  "coverage": {
    "id": "uuid",
    "type": "collision",
    "limit": 50000,
    "deductible": 500
  }
}
```

### Document Upload Endpoints

#### POST /api/upload
**Request:** multipart/form-data
```
file: <binary pdf/docx>
policyId: optional (uuid)
```
**Response (200):**
```json
{
  "success": true,
  "documentId": "uuid",
  "fileName": "policy_document.pdf",
  "status": "processing",
  "message": "Document uploaded and processing..."
}
```

### AI Chat Endpoints

#### POST /api/ai/chat
**Request:**
```json
{
  "message": "What are my coverage limits?",
  "documentId": "uuid (optional)",
  "conversationId": "uuid (optional)"
}
```
**Response (200 - Streaming):**
```json
{
  "conversationId": "uuid",
  "message": "Your coverage limits are...",
  "ragContext": {
    "retrievedChunks": [
      {
        "source": "policy_document.pdf",
        "chunk": "Coverage includes...",
        "score": 0.95
      }
    ],
    "tokensUsed": 150
  },
  "timestamp": "2026-05-02T10:30:00Z"
}
```

## 6. Security Architecture

### Authentication Flow
```
┌─────────────┐
│   Frontend  │
│ (React App) │
└──────┬──────┘
       │ POST /api/auth/login
       │ (email, password)
       ▼
┌──────────────────────┐
│ AuthController       │
│                      │
│ 1. Validate creds    │
│ 2. Hash password     │
│ 3. Generate JWT      │
└──────┬───────────────┘
       │
       ▼
┌──────────────────┐
│ JWT Token        │
│ (HS256, exp:24h) │
└──────┬───────────┘
       │
       ▼
┌──────────────────────┐
│ Store in localStorage│
│ (frontend)           │
│ Include in headers   │
└──────┬───────────────┘
       │
       ▼ Subsequent requests
┌──────────────────────┐
│ AuthMiddleware       │
│                      │
│ 1. Extract token     │
│ 2. Validate signature│
│ 3. Check expiration  │
│ 4. Set user context  │
└──────┬───────────────┘
       │
       ▼
   Authorized
   Request
```

### Data Protection
- **In Transit:** HTTPS/TLS 1.2+
- **At Rest:** 
  - Blob Storage: Encryption at rest
  - Cognitive Search: Encrypted indexes
  - JWT tokens: HS256 signature
- **Sensitive Data:** Passwords hashed with bcrypt/PBKDF2

### Authorization
- Role-based access control (RBAC)
  - Customer: View own policies, upload own documents
  - Admin (future): Manage all customers and policies
- Endpoint authorization via JWT claims

## 7. Deployment Architecture

### Development Environment
```
Local Machine
├── Backend: dotnet run
├── Frontend: npm start
└── Mock Azure services (emulators)
```

### Staging Environment
```
Azure Subscription
├── App Service (Backend)
├── Static Web Apps (Frontend)
├── Storage Account (Blob)
├── Cognitive Search
├── OpenAI (playground)
└── Communication Services
```

### Production Environment
```
Azure Subscription (Production)
├── App Service (Auto-scaling)
├── Static Web Apps (CDN)
├── Storage Account (Geo-redundant)
├── Cognitive Search (High availability)
├── OpenAI (Production)
├── Communication Services
├── Application Insights (Monitoring)
└── Azure KeyVault (Secrets)
```

### CI/CD Pipeline
```
GitHub Repository
    │
    ├─ Commit to main branch
    │       │
    │       ▼
    │   GitHub Actions
    │   ├─ Build Backend (.NET)
    │   ├─ Run Unit Tests
    │   ├─ Build Frontend (React)
    │   └─ SonarQube Analysis
    │
    └─ Deploy to Azure
        ├─ Backend → App Service
        ├─ Frontend → Static Web Apps
        └─ Smoke Tests
```

## 8. Error Handling & Logging

### Backend Error Handling
```
Try-Catch Blocks
    │
    ├─ Validation Error (400)
    ├─ Authentication Error (401)
    ├─ Authorization Error (403)
    ├─ Not Found Error (404)
    ├─ Server Error (500)
    └─ Service Unavailable (503)

All errors logged to:
├─ Application Insights
├─ Local logs file
└─ Console (development)
```

### Frontend Error Handling
- API error interceptors
- User-friendly error messages
- Toast notifications
- Error boundary components
- Retry mechanisms for failed requests

## 9. Scalability Considerations

### Backend Scaling
- Azure App Service auto-scaling (CPU/Memory triggers)
- Connection pooling for Cognitive Search
- Rate limiting on API endpoints
- Request queuing for heavy operations

### Frontend Optimization
- Code splitting and lazy loading
- Image optimization
- CSS/JS minification
- CDN distribution via Static Web Apps

### Database Scaling (Future)
- Cache layer (Azure Cache for Redis)
- Read replicas for Cognitive Search
- Partition strategies for large datasets

## 10. Monitoring & Observability

### Metrics to Track
- API response time
- Error rates by endpoint
- AI chat latency (including RAG retrieval)
- Document processing time
- Token usage (OpenAI costs)
- Notification delivery rates

### Logging
- All API requests/responses
- AI interactions (prompts, responses, context)
- Document processing steps
- User actions (login, policy changes)
- System errors and exceptions

### Alerting
- High error rate (>5%)
- API latency >5s
- Document processing failures
- OpenAI quota warnings
- Service health checks

---

This design document provides a comprehensive blueprint for implementing the Auto Insurance AI Self-Service Portal. Refer to `plan.md` for the implementation roadmap.
