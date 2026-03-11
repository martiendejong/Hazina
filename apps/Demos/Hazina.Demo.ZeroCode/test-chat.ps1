#!/usr/bin/env pwsh
# Test script for Zero-Code API RAG Chat endpoint

$baseUrl = "https://localhost:49238"

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  ZERO-CODE API - RAG CHAT TEST" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""

# Trust the self-signed certificate (for HTTPS testing)
if (-not ([System.Management.Automation.PSTypeName]'ServerCertificateValidationCallback').Type) {
    $certCallback = @"
    using System;
    using System.Net;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    public class ServerCertificateValidationCallback {
        public static void Ignore() {
            if(ServicePointManager.ServerCertificateValidationCallback == null) {
                ServicePointManager.ServerCertificateValidationCallback +=
                    delegate(Object obj, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors) {
                        return true;
                    };
            }
        }
    }
"@
    Add-Type $certCallback
}
[ServerCertificateValidationCallback]::Ignore()

Write-Host "Step 1: Creating test documents..." -ForegroundColor Yellow

# Create Document 1
$doc1 = @{
    title = "Getting Started with Hazina"
    content = "Hazina is a framework for building enterprise applications with built-in RAG capabilities. It provides dynamic API generation from YAML configuration."
    category = "Tutorial"
} | ConvertTo-Json

try {
    $response1 = Invoke-RestMethod -Uri "$baseUrl/api/Document" -Method Post -Body $doc1 -ContentType "application/json"
    Write-Host "  ✓ Created document: Getting Started with Hazina" -ForegroundColor Green
} catch {
    Write-Host "  ✗ Error creating document 1: $_" -ForegroundColor Red
}

# Create Document 2
$doc2 = @{
    title = "Zero-Code API Features"
    content = "The Zero-Code API allows you to define entities in YAML without writing any C# code. It automatically generates CRUD endpoints, search functionality, and supports embeddings for RAG."
    category = "Documentation"
} | ConvertTo-Json

try {
    $response2 = Invoke-RestMethod -Uri "$baseUrl/api/Document" -Method Post -Body $doc2 -ContentType "application/json"
    Write-Host "  ✓ Created document: Zero-Code API Features" -ForegroundColor Green
} catch {
    Write-Host "  ✗ Error creating document 2: $_" -ForegroundColor Red
}

# Create Document 3
$doc3 = @{
    title = "RAG Architecture"
    content = "Retrieval-Augmented Generation combines document search with LLM responses. When you ask a question, the system searches for relevant documents and includes them as context for the AI."
    category = "Technical"
} | ConvertTo-Json

try {
    $response3 = Invoke-RestMethod -Uri "$baseUrl/api/Document" -Method Post -Body $doc3 -ContentType "application/json"
    Write-Host "  ✓ Created document: RAG Architecture" -ForegroundColor Green
} catch {
    Write-Host "  ✗ Error creating document 3: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "Step 2: Testing RAG chat..." -ForegroundColor Yellow

# Test Chat
$chatRequest = @{
    message = "How does the Zero-Code API work?"
    topK = 3
} | ConvertTo-Json

try {
    $chatResponse = Invoke-RestMethod -Uri "$baseUrl/api/chat" -Method Post -Body $chatRequest -ContentType "application/json"

    Write-Host ""
    Write-Host "=============================================" -ForegroundColor Cyan
    Write-Host "  CHAT RESPONSE" -ForegroundColor Cyan
    Write-Host "=============================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Question: How does the Zero-Code API work?" -ForegroundColor White
    Write-Host ""
    Write-Host "Answer:" -ForegroundColor Green
    Write-Host $chatResponse.answer -ForegroundColor White
    Write-Host ""
    Write-Host "Documents Used:" -ForegroundColor Green
    foreach ($doc in $chatResponse.documentsUsed) {
        Write-Host "  - $($doc.title)" -ForegroundColor White
    }
    Write-Host ""
    Write-Host "Token Usage:" -ForegroundColor Green
    Write-Host "  Prompt: $($chatResponse.tokenUsage.promptTokens)" -ForegroundColor White
    Write-Host "  Completion: $($chatResponse.tokenUsage.completionTokens)" -ForegroundColor White
    Write-Host "  Total: $($chatResponse.tokenUsage.totalTokens)" -ForegroundColor White
    Write-Host ""
    Write-Host "=============================================" -ForegroundColor Cyan
    Write-Host "  ✓ TEST SUCCESSFUL" -ForegroundColor Green
    Write-Host "=============================================" -ForegroundColor Cyan
} catch {
    Write-Host ""
    Write-Host "  ✗ Error during chat: $_" -ForegroundColor Red
    Write-Host ""
}

Write-Host ""
Write-Host "To test manually, open: $baseUrl" -ForegroundColor Yellow
Write-Host ""
