# Development Roadmap

## Current Status: ✅ MVP + Auth + Enhanced Tests

MVP funcional com auth (JWT + refresh + GitHub OAuth), backend protegido e frontend com login. Suite de testes expandida com cobertura completa de serviços de autenticação e utilitários.

---

## Phase 1: Testing & Validation ✅ (COMPLETED)

### Backend
- ✅ Unit tests for CNAB parser
- ✅ Unit tests for transaction models
- ✅ Service layer tests
- ✅ Controller tests
- ✅ CursorPaginationHelper unit tests (18 testes)
- ✅ AuthService comprehensive tests (23 testes)
- ✅ Code cleanup: removed 10+ unused imports across codebase
- **Total Test Suite**: 175 tests (41 new + 134 existing) - 100% passing

### Frontend
- ✅ Component structure
- ✅ Error handling
- ✅ Loading states

### Infrastructure
- ✅ Docker configuration
- ✅ Database initialization
- ✅ CORS setup

---

## Phase 2: Enhancement Features (🔄 IN PROGRESS)

### ⭐ Next Priority: API Quality & Documentation 📖

#### 1. **FluentValidation & Input Sanitization** 🔐
   - [ ] Install FluentValidation package
   - [ ] Create validators for:
     - [ ] AuthDtos (Register, Login, RefreshToken)
     - [ ] Transaction models
     - [ ] CPF validation (real CPF format)
   - [ ] Implement validation middleware
   - [ ] Return ProblemDetails on validation failure
   - **Estimated**: 1-2 days

#### 2. **Swagger/OpenAPI Enrichment** 📖
   - [ ] Add detailed descriptions to all endpoints
   - [ ] Document request/response examples
   - [ ] Add error code documentation (401, 403, 404, 422, 500)
   - [ ] Document authentication scheme (Bearer JWT)
   - [ ] Add security definitions
   - **Estimated**: 1 day

#### 3. **Logging & Observability** 📊
   - [ ] Implement Serilog for structured logging
   - [ ] Add correlation IDs for request tracing
   - [ ] Log authentication events (login, logout, OAuth)
   - [ ] Log transaction uploads and processing
   - [ ] Log errors with full stack traces
   - **Estimated**: 1-2 days

### Query & Escalabilidade 🔍
- [x] Cursor-based pagination implemented (CursorPaginationHelper)
- [ ] Paginação e ordenação no GET por CPF
- [ ] Filtros (data, tipo) documentados
- [ ] Índices em CPF/Data/Tipo (database optimization)

### Performance ⚡
- [ ] Caching (IMemoryCache) para consultas frequentes
- [ ] Otimizações de banco (índices adicionais, análise de planos)

### Advanced Filtering & Search 🔍
*(parte alinhada com Query & Escalabilidade acima; export permanece opcional)*
- [ ] Export transactions to CSV/Excel (opcional)
- [ ] Advanced filtering (date range, amount range, type)

### File History & Management 📋
**Estimated**: 2 days
- [ ] Track uploaded files
- [ ] Store file metadata (upload date, user, transaction count)
- [ ] Show upload history
- [ ] Ability to delete specific imports
- [ ] Bulk operations on imports

**Database Changes**:
- Create ImportFile table
- Add timestamps to transactions
- Link transactions to imports

### Batch Processing 📦
**Estimated**: 2-3 days
- [ ] Support uploading multiple files at once
- [ ] Process files asynchronously with background jobs
- [ ] Implement progress tracking
- [ ] Email notifications on completion

### Dashboard & Analytics 📊
- (posterior ao pacote de qualidade/performance)

**Libraries to Consider**:
- Chart.js or Recharts for visualizations
- date-fns for date manipulation

### Data Reconciliation ✓
- (posterior; depende de validações avançadas)

---

## Phase 3: Performance & Optimization ⚡

### Database Optimization
- [ ] Add indexes for frequently queried fields
- [ ] Implement pagination for large datasets
- [ ] Add query performance monitoring
- [ ] Implement data archiving for old transactions

### Frontend Optimization
- [ ] Implement lazy loading for transaction list
- [ ] Add virtual scrolling for large lists
- [ ] Optimize component re-renders
- [ ] Implement Redux/Context API for state management
- [ ] Code splitting and lazy loading routes

### Caching
- [ ] Implement Redis caching layer
- [ ] Cache frequently accessed reports
- [ ] API response caching

---

## Implementation Priority Matrix

### ✅ Completed (This Session)
1. ✅ Authentication & Authorization (+5 points)
2. ✅ Automated Tests (41 new tests added)
   - CursorPaginationHelper: 18 tests
   - AuthService: 23 tests
   - All passing with InMemoryDatabase for realistic testing
3. ✅ Code Cleanup: Import optimization across 10+ files

### High Priority (Next 1-2 weeks) 🔴 START HERE
1. **FluentValidation & Input Sanitization** (1-2 days)
   - Validators for all DTOs
   - ProblemDetails error responses
   
2. **Swagger/OpenAPI Enrichment** (1 day)
   - Rich endpoint descriptions
   - Request/response examples
   - Error code documentation
   
3. **Logging & Observability** (1-2 days)
   - Serilog integration
   - Correlation IDs
   - Request/response logging

### Medium Priority (Next 2-3 weeks)
1. Database Indexes & Query Optimization
2. Caching Strategy (IMemoryCache)
3. Advanced Filtering & Search
4. File History & Management (2 days)
5. Batch Processing (2-3 days)

### Low Priority (Next 1-2 months)
1. Dashboard & Analytics
2. Performance Load Testing
3. API Versioning Strategy


---

## Testing Strategy

### Unit Tests (Current)
```csharp
dotnet test backend/Tests/CnabApi.Tests.csproj
```

### Integration Tests (To Add)
- API endpoint tests
- Database transaction tests
- End-to-end scenarios

### E2E Tests (To Add)
- Playwright or Selenium for frontend tests
- Full workflow testing

### Performance Tests (To Add)
- Load testing with k6 or JMeter
- Database query performance analysis

---

## Documentation To Add

- [ ] Architecture decision records (ADR)
- [ ] System design documentation
- [ ] Database schema documentation
- [ ] API versioning strategy
- [ ] Deployment runbook
- [ ] Troubleshooting guide
- [ ] Performance tuning guide

---

### Known Limitations & Tech Debt

### ✅ Resolved
1. ✅ **Testing**: Expanded test suite from 134 to 175 tests
2. ✅ **Code Quality**: Removed unused imports across codebase
3. ✅ **Auth Testing**: Full coverage of authentication service (23 tests)
4. ✅ **Utils Testing**: Complete pagination utility testing (18 tests)

### Current Implementation (To Address)
1. **Docs**: README/API docs incompletos; Swagger sem exemplos ricos
2. **Validação**: Falta FluentValidation/ProblemDetails; CPF válido não checado
3. **Paginação/Filtros**: Parcialmente implementado (cursor-based exists, mas sem filtros na API)
4. **Caching/Performance**: Não há cache ou tuning de índices
5. **Logging/Observabilidade**: Logs básicos, sem correlação/telemetria
6. **Versionamento**: API sem versão explícita
7. **Docker**: Issues com docker-compose up (precisa debug)

---

## Success Metrics

### Performance
- [ ] API response time < 200ms (p95)
- [ ] Frontend load time < 3s
- [ ] Database query time < 100ms
- [ ] 99.9% uptime

### Quality
- [ ] Code coverage > 80%
- [ ] Zero high-severity bugs
- [ ] < 5 open issues at any time
- [ ] All tests passing

### User Experience
- [ ] User satisfaction > 4.5/5
- [ ] Error rate < 0.1%
- [ ] Zero user-reported critical bugs
- [ ] Feature adoption > 80%

---

## Timeline Estimate

| Phase | Estimated Time | Status |
|-------|----------------|--------|
| Phase 1: Testing & Validation | 1-2 weeks | ✅ Done |
| Phase 2a: Quality & Docs | 3-4 days | ⏳ **START HERE** |
| Phase 2b: Enhancement Features | 2-3 weeks | 📋 Planned |
| Phase 3: Performance & Optimization | 1-2 weeks | 📋 Planned |
| Phase 4: DevOps & Infrastructure | 2-3 weeks | 📋 Planned |
| Phase 5: Advanced Features | 3-4 weeks | 📋 Planned |
| Phase 6: Enterprise Features | 4-6 weeks | 📋 Planned |

---

## Getting Help

- Check [API_DOCUMENTATION.md](API_DOCUMENTATION.md) for API details
- Check [README.md](README.md) for setup instructions
- Check [PROJECT_SUMMARY.md](PROJECT_SUMMARY.md) for current status
- Check [GIT_COMMIT_GUIDE.md](GIT_COMMIT_GUIDE.md) for commit practices

---

## Questions? 

Create an issue in the repository with the label `question` or `enhancement`.
