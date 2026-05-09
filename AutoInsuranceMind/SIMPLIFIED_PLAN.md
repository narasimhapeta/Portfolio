# Auto Insurance AI Self-Service Portal - SIMPLIFIED PLAN (No Auth, No Tests)

## Overview
Develop a **lightweight MVP** for an auto insurance customer self-service portal with AI-powered RAG chatbot integration, using .NET Core Web API backend, Azure AI services orchestrated via Semantic Kernel, and React frontend. Includes mock data simulation, automated notifications, document upload for personalized policy analysis, and local/Azure deployment.

**Scope Changes**: 
- ❌ Skip authentication entirely
- ❌ Skip automated testing (unit/E2E tests)
- ✅ Focus on core features: policies, document upload, RAG chatbot, notifications

---

## Simplified Project Timeline

| Phase | Duration | Focus |
|-------|----------|-------|
| **Phase 1** | 2-3 hours | Backend scaffolding + Mock API endpoints |
| **Phase 2** | 2-3 hours | Frontend React app + Components |
| **Phase 3** | 1-2 hours | Document upload + File handling |
| **Phase 4** | 1-2 hours | Semantic Kernel + RAG integration |
| **Phase 5** | 1 hour | Notifications + Email/SMS simulation |
| **Phase 6** | 1 hour | Polish, styling, bug fixes |
| | **~8-10 hours** | **TOTAL: Day 1 Complete MVP** |

---

## PHASE 1: Backend Scaffolding (2-3 hours)

### 1.1 Initialize .NET Project
```bash
cd API
dotnet new webapi -n AutoInsuranceMind.API
cd AutoInsuranceMind.API
dotnet add package Microsoft.SemanticKernel
dotnet add package Azure.Search.Documents
dotnet add package Azure.AI.OpenAI
dotnet add package Azure.Storage.Blobs
```

### 1.2 Project Structure
```
API/
├── Controllers/
│   ├── PoliciesController.cs
│   ├── UploadController.cs
│   ├── AIController.cs
│   └── NotificationsController.cs
├── Services/
│   ├── PolicyService.cs
│   ├── DocumentService.cs
│   ├── AIService.cs
│   └── NotificationService.cs
├── Models/
│   ├── Policy.cs
│   ├── Coverage.cs
│   ├── UploadedDocument.cs
│   ├── ChatMessage.cs
│   └── Customer.cs
├── Data/
│   └── MockDataStore.cs
├── Program.cs
└── appsettings.json
```

### 1.3 Core Models
```csharp
// Models/Customer.cs
public class Customer
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
}

// Models/Policy.cs
public class Policy
{
    public string Id { get; set; }
    public string CustomerId { get; set; }
    public string PolicyNumber { get; set; }
    public string Type { get; set; } // auto, home
    public string Status { get; set; } // active, expired
    public decimal Premium { get; set; }
    public List<Coverage> Coverages { get; set; }
}

// Models/Coverage.cs
public class Coverage
{
    public string Id { get; set; }
    public string Type { get; set; } // liability, collision
    public decimal Limit { get; set; }
    public decimal Deductible { get; set; }
}

// Models/ChatMessage.cs
public class ChatMessage
{
    public string Id { get; set; }
    public string CustomerId { get; set; }
    public string Message { get; set; }
    public string Response { get; set; }
    public List<string> Sources { get; set; }
    public DateTime Timestamp { get; set; }
}

// Models/UploadedDocument.cs
public class UploadedDocument
{
    public string Id { get; set; }
    public string CustomerId { get; set; }
    public string FileName { get; set; }
    public string FilePath { get; set; } // local storage
    public string ExtractedText { get; set; }
    public DateTime UploadedAt { get; set; }
}
```

### 1.4 Mock Data Store
```csharp
// Data/MockDataStore.cs
public static class MockDataStore
{
    public static List<Customer> Customers = new()
    {
        new Customer { Id = "cust-001", Name = "John Doe", Email = "john@example.com" },
        new Customer { Id = "cust-002", Name = "Jane Smith", Email = "jane@example.com" }
    };

    public static List<Policy> Policies = new()
    {
        new Policy
        {
            Id = "pol-001",
            CustomerId = "cust-001",
            PolicyNumber = "AUTO-2026-001",
            Type = "auto",
            Status = "active",
            Premium = 1200.00m,
            Coverages = new List<Coverage>
            {
                new Coverage { Id = "cov-001", Type = "liability", Limit = 100000, Deductible = 500 },
                new Coverage { Id = "cov-002", Type = "collision", Limit = 50000, Deductible = 1000 }
            }
        }
    };

    public static List<UploadedDocument> Documents = new();
    public static List<ChatMessage> ChatHistory = new();
}
```

### 1.5 Controllers (Minimal)
```csharp
// Controllers/PoliciesController.cs
[ApiController]
[Route("api/[controller]")]
public class PoliciesController : ControllerBase
{
    [HttpGet]
    public IActionResult GetPolicies()
    {
        var policies = MockDataStore.Policies;
        return Ok(new { policies });
    }

    [HttpGet("{id}")]
    public IActionResult GetPolicy(string id)
    {
        var policy = MockDataStore.Policies.FirstOrDefault(p => p.Id == id);
        return policy == null ? NotFound() : Ok(policy);
    }

    [HttpPut("{id}/coverages/{covId}")]
    public IActionResult UpdateCoverage(string id, string covId, [FromBody] Coverage coverage)
    {
        var policy = MockDataStore.Policies.FirstOrDefault(p => p.Id == id);
        if (policy == null) return NotFound();

        var cov = policy.Coverages.FirstOrDefault(c => c.Id == covId);
        if (cov == null) return NotFound();

        cov.Limit = coverage.Limit;
        cov.Deductible = coverage.Deductible;

        return Ok(new { message = "Coverage updated", coverage = cov });
    }
}

// Controllers/UploadController.cs
[ApiController]
[Route("api/[controller]")]
public class UploadController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> UploadDocument(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded");

        var doc = new UploadedDocument
        {
            Id = Guid.NewGuid().ToString(),
            FileName = file.FileName,
            UploadedAt = DateTime.UtcNow
        };

        MockDataStore.Documents.Add(doc);
        return Ok(new { message = "Document uploaded", document = doc });
    }

    [HttpGet("documents")]
    public IActionResult GetDocuments()
    {
        return Ok(new { documents = MockDataStore.Documents });
    }
}

// Controllers/AIController.cs
[ApiController]
[Route("api/[controller]")]
public class AIController : ControllerBase
{
    private readonly AIService _aiService;

    public AIController(AIService aiService)
    {
        _aiService = aiService;
    }

    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        var response = await _aiService.ProcessMessageAsync(request.Message);
        
        var chatMsg = new ChatMessage
        {
            Id = Guid.NewGuid().ToString(),
            Message = request.Message,
            Response = response,
            Timestamp = DateTime.UtcNow
        };

        MockDataStore.ChatHistory.Add(chatMsg);
        return Ok(chatMsg);
    }
}

public class ChatRequest
{
    public string Message { get; set; }
}
```

### 1.6 Program.cs Setup
```csharp
var builder = WebApplicationBuilder.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddScoped<AIService>();
builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<NotificationService>();

var app = builder.Build();

// Middleware
app.UseHttpsRedirection();
app.UseCors("AllowLocalhost");
app.UseAuthorization();
app.MapControllers();

app.Run();
```

---

## PHASE 2: Frontend Scaffolding (2-3 hours)

### 2.1 Create React App
```bash
cd UI
npx create-react-app . --template typescript
npm install axios react-router-dom
```

### 2.2 Project Structure
```
UI/src/
├── pages/
│   ├── PolicyDashboard.tsx
│   └── NotFound.tsx
├── components/
│   ├── PolicyCard.tsx
│   ├── ChatBot.tsx
│   ├── FileUpload.tsx
│   ├── Navigation.tsx
│   └── Modal.tsx
├── services/
│   ├── apiClient.ts
│   ├── policyService.ts
│   ├── chatService.ts
│   └── uploadService.ts
├── types/
│   ├── policy.ts
│   └── chat.ts
├── App.tsx
└── index.tsx
```

### 2.3 API Client
```typescript
// src/services/apiClient.ts
import axios from 'axios';

const API_BASE_URL = 'http://localhost:5000/api';

const apiClient = axios.create({
    baseURL: API_BASE_URL,
    headers: {
        'Content-Type': 'application/json',
    },
});

export default apiClient;
```

### 2.4 Services
```typescript
// src/services/policyService.ts
import apiClient from './apiClient';

export const getPolicies = async () => {
    const response = await apiClient.get('/policies');
    return response.data.policies;
};

export const getPolicy = async (id: string) => {
    const response = await apiClient.get(`/policies/${id}`);
    return response.data;
};

export const updateCoverage = async (policyId: string, covId: string, coverage: any) => {
    const response = await apiClient.put(`/policies/${policyId}/coverages/${covId}`, coverage);
    return response.data;
};

// src/services/chatService.ts
import apiClient from './apiClient';

export const sendMessage = async (message: string) => {
    const response = await apiClient.post('/ai/chat', { message });
    return response.data;
};

// src/services/uploadService.ts
import apiClient from './apiClient';

export const uploadDocument = async (file: File) => {
    const formData = new FormData();
    formData.append('file', file);
    const response = await apiClient.post('/upload', formData, {
        headers: { 'Content-Type': 'multipart/form-data' },
    });
    return response.data;
};

export const getDocuments = async () => {
    const response = await apiClient.get('/upload/documents');
    return response.data.documents;
};
```

### 2.5 Components
```typescript
// src/types/policy.ts
export interface Policy {
    id: string;
    policyNumber: string;
    type: string;
    status: string;
    premium: number;
    coverages: Coverage[];
}

export interface Coverage {
    id: string;
    type: string;
    limit: number;
    deductible: number;
}

// src/components/PolicyCard.tsx
import React, { useState } from 'react';
import { Coverage } from '../types/policy';

interface PolicyCardProps {
    policyId: string;
    policyNumber: string;
    type: string;
    premium: number;
    coverages: Coverage[];
    onCoverageUpdate: (policyId: string, covId: string, coverage: Coverage) => void;
}

export const PolicyCard: React.FC<PolicyCardProps> = ({
    policyId,
    policyNumber,
    type,
    premium,
    coverages,
    onCoverageUpdate,
}) => {
    const [showModal, setShowModal] = useState(false);
    const [selectedCoverage, setSelectedCoverage] = useState<Coverage | null>(null);

    return (
        <div className="policy-card">
            <h3>{policyNumber}</h3>
            <p>Type: {type}</p>
            <p>Premium: ${premium}</p>
            <h4>Coverages:</h4>
            <ul>
                {coverages.map((cov) => (
                    <li key={cov.id}>
                        {cov.type}: ${cov.limit} (Deductible: ${cov.deductible})
                        <button onClick={() => { setSelectedCoverage(cov); setShowModal(true); }}>
                            Edit
                        </button>
                    </li>
                ))}
            </ul>
            {showModal && selectedCoverage && (
                <Modal onClose={() => setShowModal(false)}>
                    <EditCoverageForm
                        coverage={selectedCoverage}
                        onSubmit={(updated) => {
                            onCoverageUpdate(policyId, selectedCoverage.id, updated);
                            setShowModal(false);
                        }}
                    />
                </Modal>
            )}
        </div>
    );
};

// src/components/ChatBot.tsx
import React, { useState, useEffect } from 'react';
import { sendMessage } from '../services/chatService';

export const ChatBot: React.FC = () => {
    const [messages, setMessages] = useState<{ role: string; text: string }[]>([]);
    const [input, setInput] = useState('');
    const [loading, setLoading] = useState(false);

    const handleSend = async () => {
        if (!input.trim()) return;

        setMessages([...messages, { role: 'user', text: input }]);
        setInput('');
        setLoading(true);

        try {
            const response = await sendMessage(input);
            setMessages((prev) => [...prev, { role: 'ai', text: response.response }]);
        } catch (error) {
            setMessages((prev) => [...prev, { role: 'error', text: 'Failed to get response' }]);
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="chatbot">
            <div className="messages">
                {messages.map((msg, idx) => (
                    <div key={idx} className={`message ${msg.role}`}>
                        {msg.text}
                    </div>
                ))}
                {loading && <div className="message loading">AI is thinking...</div>}
            </div>
            <div className="input-area">
                <input
                    value={input}
                    onChange={(e) => setInput(e.target.value)}
                    onKeyPress={(e) => e.key === 'Enter' && handleSend()}
                    placeholder="Ask about your policy..."
                    disabled={loading}
                />
                <button onClick={handleSend} disabled={loading}>
                    Send
                </button>
            </div>
        </div>
    );
};

// src/components/FileUpload.tsx
import React, { useState } from 'react';
import { uploadDocument } from '../services/uploadService';

export const FileUpload: React.FC = () => {
    const [file, setFile] = useState<File | null>(null);
    const [loading, setLoading] = useState(false);
    const [message, setMessage] = useState('');

    const handleUpload = async () => {
        if (!file) {
            setMessage('Please select a file');
            return;
        }

        setLoading(true);
        try {
            const response = await uploadDocument(file);
            setMessage(`File uploaded: ${response.document.fileName}`);
            setFile(null);
        } catch (error) {
            setMessage('Upload failed');
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="file-upload">
            <h3>Upload Policy Document</h3>
            <input
                type="file"
                onChange={(e) => setFile(e.target.files?.[0] || null)}
                disabled={loading}
            />
            <button onClick={handleUpload} disabled={loading}>
                Upload
            </button>
            {message && <p>{message}</p>}
        </div>
    );
};

// src/pages/PolicyDashboard.tsx
import React, { useEffect, useState } from 'react';
import { Policy } from '../types/policy';
import { getPolicies, updateCoverage } from '../services/policyService';
import { PolicyCard } from '../components/PolicyCard';
import { ChatBot } from '../components/ChatBot';
import { FileUpload } from '../components/FileUpload';

export const PolicyDashboard: React.FC = () => {
    const [policies, setPolicies] = useState<Policy[]>([]);
    const [loading, setLoading] = useState(false);

    useEffect(() => {
        loadPolicies();
    }, []);

    const loadPolicies = async () => {
        setLoading(true);
        try {
            const data = await getPolicies();
            setPolicies(data);
        } catch (error) {
            console.error('Failed to load policies', error);
        } finally {
            setLoading(false);
        }
    };

    const handleCoverageUpdate = async (policyId: string, covId: string, coverage: any) => {
        try {
            await updateCoverage(policyId, covId, coverage);
            loadPolicies(); // Refresh
        } catch (error) {
            console.error('Update failed', error);
        }
    };

    return (
        <div className="dashboard">
            <h1>Auto Insurance Portal</h1>
            
            <div className="layout">
                <div className="left-panel">
                    <h2>Your Policies</h2>
                    {loading ? (
                        <p>Loading...</p>
                    ) : (
                        policies.map((policy) => (
                            <PolicyCard
                                key={policy.id}
                                policyId={policy.id}
                                policyNumber={policy.policyNumber}
                                type={policy.type}
                                premium={policy.premium}
                                coverages={policy.coverages}
                                onCoverageUpdate={handleCoverageUpdate}
                            />
                        ))
                    )}
                </div>

                <div className="right-panel">
                    <FileUpload />
                    <ChatBot />
                </div>
            </div>
        </div>
    );
};

// src/App.tsx
import React from 'react';
import { PolicyDashboard } from './pages/PolicyDashboard';
import './App.css';

function App() {
    return <PolicyDashboard />;
}

export default App;
```

### 2.6 Basic Styling
```css
/* src/App.css */
* {
    margin: 0;
    padding: 0;
    box-sizing: border-box;
}

body {
    font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
    background: #f5f5f5;
}

.dashboard {
    max-width: 1400px;
    margin: 0 auto;
    padding: 20px;
}

.layout {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 20px;
    margin-top: 20px;
}

.policy-card {
    background: white;
    padding: 20px;
    border-radius: 8px;
    box-shadow: 0 2px 4px rgba(0,0,0,0.1);
    margin-bottom: 15px;
}

.policy-card h3 {
    color: #0066cc;
    margin-bottom: 10px;
}

.policy-card p {
    margin: 5px 0;
    color: #333;
}

.policy-card ul {
    list-style: none;
    margin-top: 10px;
}

.policy-card li {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 8px;
    background: #f9f9f9;
    border-radius: 4px;
    margin-bottom: 5px;
}

.chatbot {
    background: white;
    padding: 20px;
    border-radius: 8px;
    box-shadow: 0 2px 4px rgba(0,0,0,0.1);
    display: flex;
    flex-direction: column;
    height: 500px;
}

.messages {
    flex: 1;
    overflow-y: auto;
    margin-bottom: 15px;
    border: 1px solid #ddd;
    padding: 10px;
    border-radius: 4px;
}

.message {
    margin-bottom: 10px;
    padding: 10px;
    border-radius: 4px;
}

.message.user {
    background: #0066cc;
    color: white;
    text-align: right;
}

.message.ai {
    background: #e8f4f8;
    color: #333;
}

.message.error {
    background: #ffe8e8;
    color: #cc0000;
}

.message.loading {
    font-style: italic;
    color: #999;
}

.input-area {
    display: flex;
    gap: 10px;
}

.input-area input {
    flex: 1;
    padding: 10px;
    border: 1px solid #ddd;
    border-radius: 4px;
    font-size: 14px;
}

.input-area button,
button {
    background: #0066cc;
    color: white;
    border: none;
    padding: 10px 20px;
    border-radius: 4px;
    cursor: pointer;
    font-size: 14px;
}

button:hover {
    background: #0052a3;
}

button:disabled {
    background: #ccc;
    cursor: not-allowed;
}

.file-upload {
    background: white;
    padding: 20px;
    border-radius: 8px;
    box-shadow: 0 2px 4px rgba(0,0,0,0.1);
    margin-bottom: 20px;
}

.file-upload input {
    display: block;
    margin: 10px 0;
    padding: 10px;
    border: 1px solid #ddd;
    border-radius: 4px;
}
```

---

## PHASE 3: Document Upload (1-2 hours)

### 3.1 Document Service
```csharp
// Services/DocumentService.cs
public class DocumentService
{
    private const string UploadFolder = "uploads";

    public DocumentService()
    {
        if (!Directory.Exists(UploadFolder))
            Directory.CreateDirectory(UploadFolder);
    }

    public async Task<UploadedDocument> ProcessUploadAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("File is empty");

        var filePath = Path.Combine(UploadFolder, file.FileName);
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // For now, store file path and file name
        // In Phase 4, we'll extract text and create embeddings
        var extractedText = await ExtractTextAsync(filePath);

        var document = new UploadedDocument
        {
            Id = Guid.NewGuid().ToString(),
            FileName = file.FileName,
            FilePath = filePath,
            ExtractedText = extractedText,
            UploadedAt = DateTime.UtcNow
        };

        return document;
    }

    private async Task<string> ExtractTextAsync(string filePath)
    {
        // Simple extraction: for PDF/DOCX, use placeholder
        // In production, use Azure Document Intelligence
        if (filePath.EndsWith(".pdf"))
        {
            return "Sample policy document: Auto insurance coverage includes liability, collision, and comprehensive.";
        }
        else if (filePath.EndsWith(".txt"))
        {
            return await File.ReadAllTextAsync(filePath);
        }
        return "Unable to extract text";
    }
}
```

---

## PHASE 4: Semantic Kernel + RAG (1-2 hours)

### 4.1 AI Service with Mock RAG
```csharp
// Services/AIService.cs
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

public class AIService
{
    private Kernel _kernel;
    private IChatCompletionService _chatCompletion;

    public AIService(IConfiguration config)
    {
        // Initialize Semantic Kernel
        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion("gpt-4o-mini", config["OpenAI:ApiKey"]);
        _kernel = builder.Build();
        _chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
    }

    public async Task<string> ProcessMessageAsync(string userMessage)
    {
        try
        {
            // Retrieve relevant documents (mock RAG)
            var relevantDocs = RetrieveRelevantDocuments(userMessage);

            // Build prompt with context
            var systemPrompt = @"You are a helpful insurance assistant. Answer questions about auto insurance policies based on the provided documents.";
            
            var contextPrompt = relevantDocs.Any() 
                ? $"Based on the following policy documents:\n{string.Join("\n", relevantDocs)}\n\nUser: {userMessage}"
                : $"User: {userMessage}";

            // Call OpenAI via Semantic Kernel
            var chatMessages = new List<ChatMessageContent>
            {
                new ChatMessageContent(AuthorRole.System, systemPrompt),
                new ChatMessageContent(AuthorRole.User, contextPrompt)
            };

            var response = await _chatCompletion.GetChatMessageContentAsync(chatMessages);
            return response.Content;
        }
        catch (Exception ex)
        {
            return $"Error processing message: {ex.Message}";
        }
    }

    private List<string> RetrieveRelevantDocuments(string query)
    {
        // Mock RAG: Search documents for relevant chunks
        var relevantDocs = new List<string>();

        foreach (var doc in MockDataStore.Documents)
        {
            if (doc.ExtractedText != null && 
                doc.ExtractedText.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                relevantDocs.Add(doc.ExtractedText);
            }
        }

        return relevantDocs;
    }
}
```

### 4.2 appsettings.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "OpenAI": {
    "ApiKey": "YOUR_OPENAI_API_KEY",
    "Model": "gpt-4o-mini"
  },
  "AllowedHosts": "*"
}
```

---

## PHASE 5: Notifications (1 hour)

### 5.1 Notification Service
```csharp
// Services/NotificationService.cs
public class NotificationService
{
    public async Task SendEmailNotificationAsync(string email, string message)
    {
        // Mock: Log to console
        Console.WriteLine($"[EMAIL] To: {email}");
        Console.WriteLine($"[EMAIL] Message: {message}");
        await Task.Delay(100); // Simulate async operation
    }

    public async Task SendSMSNotificationAsync(string phoneNumber, string message)
    {
        // Mock: Log to console
        Console.WriteLine($"[SMS] To: {phoneNumber}");
        Console.WriteLine($"[SMS] Message: {message}");
        await Task.Delay(100); // Simulate async operation
    }

    public async Task SendPolicyRenewalReminder(string customerId)
    {
        var customer = MockDataStore.Customers.FirstOrDefault(c => c.Id == customerId);
        if (customer != null)
        {
            var message = $"Dear {customer.Name}, your policy is expiring soon. Please renew to avoid coverage lapse.";
            await SendEmailNotificationAsync(customer.Email, message);
        }
    }
}
```

### 5.2 Notification Controller
```csharp
// Controllers/NotificationsController.cs
[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly NotificationService _notificationService;

    public NotificationsController(NotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpPost("send-renewal-reminder")]
    public async Task<IActionResult> SendRenewalReminder(string customerId)
    {
        await _notificationService.SendPolicyRenewalReminder(customerId);
        return Ok(new { message = "Reminder sent" });
    }
}
```

---

## PHASE 6: Styling & Polish (1 hour)

### Focus Areas:
1. Make UI responsive (mobile-friendly)
2. Add loading states
3. Better error messages
4. Simple color scheme
5. Improve layout and spacing

---

## Running the Project

### Backend
```bash
cd API/AutoInsuranceMind.API
dotnet run
# Runs on http://localhost:5000
```

### Frontend
```bash
cd UI
npm start
# Runs on http://localhost:3000
```

### Test
1. Open http://localhost:3000
2. View policies (should show mock data)
3. Edit a coverage
4. Upload a document
5. Ask AI chatbot questions
6. See responses from mock AI

---

## What's NOT Included (Yet)

✗ Real Azure OpenAI integration (easy to add later)
✗ Real Cognitive Search (easy to add later)
✗ Real Blob Storage (easy to add later)
✗ Authentication
✗ Unit/E2E tests
✗ Real email/SMS (mocked)
✗ Document Intelligence extraction (text is mocked)
✗ Database (using in-memory mock)
✗ Deployment to Azure

---

## Quick Upgrade Path (Days 2-3)

**Day 2: Add Real Azure Services**
```
1. Create Azure OpenAI deployment
2. Create Azure Cognitive Search instance
3. Replace mock AI service with real OpenAI calls
4. Add real document text extraction
5. Test RAG with real services
```

**Day 3: Deploy to Azure**
```
1. Deploy backend to App Service
2. Deploy frontend to Static Web Apps
3. Configure environment variables
4. Test live application
```

---

## File Structure Summary

```
AutoInsuranceMind/
├── plan.md (original plan)
├── design.md (detailed design)
├── review.md (review notes)
├── SIMPLIFIED_PLAN.md (this file)
├── API/
│   ├── Controllers/ (4 files)
│   ├── Services/ (4 files)
│   ├── Models/ (5 files)
│   ├── Data/
│   │   └── MockDataStore.cs
│   ├── Program.cs
│   ├── appsettings.json
│   └── AutoInsuranceMind.API.csproj
└── UI/
    └── src/
        ├── pages/ (2 files)
        ├── components/ (4 files)
        ├── services/ (4 files)
        ├── types/ (2 files)
        ├── App.tsx
        ├── App.css
        └── index.tsx
```

---

## Success Criteria (Day 1)

✅ Backend API runs without errors
✅ Frontend React app loads in browser
✅ Can view mock policies
✅ Can edit coverage and see updates
✅ Can upload a document
✅ Can chat with mock AI and get responses
✅ No authentication needed
✅ No tests automated
✅ All features connected end-to-end

**Ready to build? Let me scaffold the code now!** 🚀
