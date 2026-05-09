# Auto Insurance AI Self-Service Portal - Capstone Project Plan

## Overview
Develop a basic MVP for an auto insurance customer self-service portal with AI-powered RAG chatbot integration, using .NET Core Web API backend, Azure AI services orchestrated via Semantic Kernel, and React frontend. Includes mock data simulation, automated notifications, document upload for personalized policy analysis, and deployment to Azure App Service.

## Project Architecture

```
AutoInsuranceMind/
├── API/                          # .NET Core Web API
│   ├── Controllers/
│   │   ├── PoliciesController.cs
│   │   ├── UploadController.cs
│   │   └── AIController.cs
│   ├── Services/
│   │   ├── AIService.cs
│   │   └── DocumentService.cs
│   ├── Models/
│   ├── appsettings.json
│   └── AutoInsuranceMind.API.csproj
└── UI/                           # React TypeScript Frontend
    ├── src/
    │   ├── components/
    │   │   ├── ChatBot.tsx
    │   │   └── FileUpload.tsx
    │   ├── pages/
    │   │   ├── Login.tsx
    │   │   └── PolicyDashboard.tsx
    │   └── App.tsx
    └── package.json
```

## Phase 1: Project Setup and Infrastructure
1. Initialize .NET Core Web API project in API/ folder with controllers for policies, customers, AI endpoints, and file uploads.
2. Set up React TypeScript project in UI/ folder with basic routing and components for portal dashboard, chat, and file upload.
3. Configure Azure resources:
   - App Service for deployment
   - Azure OpenAI for AI chat and embeddings
   - Azure Blob Storage for document uploads
   - Azure Cognitive Search for vector search (RAG)
   - Azure Communication Services for notifications (email/SMS)
4. Add Semantic Kernel to backend for AI orchestration, memory (for RAG), and plugins.
5. Set up mock data storage (in-memory or SQLite) for policies, customers, and claims.

## Phase 2: Backend Development
6. Implement customer authentication (basic JWT or Azure AD mock).
7. Create API endpoints for viewing/managing policies and coverages.
8. Develop file upload endpoint:
   - Accept policy documents
   - Store in Azure Blob Storage
   - Process with Azure Document Intelligence for text extraction
   - Vectorize using Azure OpenAI embeddings
   - Index in Azure Cognitive Search for RAG
9. Develop AI RAG chatbot endpoint using Semantic Kernel:
   - Integrate Azure OpenAI for conversational AI with retrieval from uploaded documents
   - Add plugin for policy document analysis and question answering
10. Implement automation workflows:
    - Policy renewal reminders via Azure Communication Services
    - Email/SMS notifications for policy updates
11. Add logging and error handling.

## Phase 3: Frontend Development
12. Build React components:
    - Login page
    - Policy dashboard (view/manage coverages)
    - File upload component for policy documents
    - Chat interface for AI RAG assistant
13. Integrate API calls for:
    - Fetching policies
    - Updating coverages
    - Uploading documents
    - Chatting with AI (including RAG responses)
14. Style the UI with a simple, responsive design (using CSS or Material-UI).
15. Add client-side validation and error handling for file uploads.

## Phase 4: Integration and Testing
16. Connect frontend to backend APIs, ensure CORS is configured.
17. Test document upload:
    - Verify files are stored
    - Verify files are processed and indexed for RAG
18. Test AI RAG chatbot:
    - Upload policy doc
    - Ask questions
    - Verify responses are retrieved and generated from the document
19. Test automation:
    - Simulate renewal notifications and policy change alerts
20. Perform end-to-end testing:
    - User login
    - Upload doc
    - View policy
    - Chat with AI
    - Receive notifications
21. Validate deployment readiness: build and test locally.

## Phase 5: Deployment and Finalization
22. Deploy backend to Azure App Service.
23. Deploy frontend to Azure Static Web Apps or App Service.
24. Configure environment variables for Azure services (keys, endpoints).
25. Document the project: README with setup, features, and architecture overview.

## Key Technologies

### Backend
- **.NET Core 8/9** - Web API framework
- **Semantic Kernel** - AI orchestration and RAG
- **Azure OpenAI** - LLM for chatbot and embeddings
- **Azure Blob Storage** - Document storage
- **Azure Cognitive Search** - Vector search for RAG
- **Azure Document Intelligence** - Document text extraction
- **Azure Communication Services** - Email/SMS notifications

### Frontend
- **React 18+** - UI framework
- **TypeScript** - Type safety
- **Material-UI or Tailwind CSS** - Styling
- **Axios/Fetch API** - HTTP client

### Infrastructure
- **Azure App Service** - Hosting
- **Azure Static Web Apps** - Frontend hosting (alternative)
- **Azure Resource Group** - Resource management

## Features

### Core Features (MVP)
- ✅ Customer authentication
- ✅ View/manage policies and coverages
- ✅ Upload policy documents
- ✅ RAG-powered AI chatbot for policy questions
- ✅ Policy renewal reminders (email/SMS)
- ✅ Policy update notifications

### Future Enhancements
- Claims filing and tracking
- Real-time policy pricing
- Fraud detection
- Advanced analytics
- Multi-language support
- Mobile app

## Data Models

### Customer
```
{
  id: string,
  name: string,
  email: string,
  phoneNumber: string,
  policies: Policy[]
}
```

### Policy
```
{
  id: string,
  customerId: string,
  policyNumber: string,
  type: string (auto/home/etc),
  startDate: date,
  endDate: date,
  coverages: Coverage[],
  premium: decimal,
  status: string (active/expired/etc)
}
```

### Coverage
```
{
  id: string,
  policyId: string,
  type: string (liability/collision/etc),
  limit: decimal,
  deductible: decimal
}
```

### UploadedDocument
```
{
  id: string,
  customerId: string,
  fileName: string,
  blobUrl: string,
  uploadedAt: date,
  vectorIndexId: string
}
```

## API Endpoints

### Authentication
- `POST /api/auth/login` - Customer login
- `POST /api/auth/logout` - Customer logout

### Policies
- `GET /api/policies` - Get customer policies
- `GET /api/policies/{id}` - Get policy details
- `PUT /api/policies/{id}` - Update policy
- `GET /api/policies/{id}/coverages` - Get coverages
- `PUT /api/policies/{id}/coverages/{covId}` - Update coverage

### Documents
- `POST /api/upload` - Upload policy document
- `GET /api/upload/documents` - List uploaded documents
- `GET /api/upload/documents/{id}` - Get document details
- `DELETE /api/upload/documents/{id}` - Delete document

### AI Chatbot (RAG)
- `POST /api/ai/chat` - Send message to RAG chatbot
- `GET /api/ai/chat/history` - Get chat history
- `POST /api/ai/reset` - Clear chat context

## Verification Checklist

- [ ] Backend API initialized with mock data
- [ ] Frontend React app scaffolded
- [ ] Authentication implemented (JWT/Azure AD)
- [ ] Document upload working (files stored in Blob)
- [ ] Document processing pipeline working (extraction + vectorization)
- [ ] RAG indexing working (Cognitive Search indexed)
- [ ] AI chatbot endpoint working (Semantic Kernel + OpenAI)
- [ ] Frontend components communicating with backend
- [ ] E2E test: Upload doc → Ask question → Get RAG answer
- [ ] Automation: Renewal reminder triggered and sent
- [ ] CORS configured correctly
- [ ] Deployment to Azure tested locally
- [ ] Documentation complete

## Decisions

- Use mock data for simplicity, no real database integration
- AI focused on RAG chatbot with document upload; no advanced fraud detection
- Basic authentication; expand to Azure AD if needed later
- Scope to core features: view/manage policies, document upload, RAG AI chat, notifications
- Azure AI services: OpenAI for chat/embeddings, Cognitive Search for RAG, Blob Storage for docs, Communication Services for notifications; Document Intelligence for text extraction
