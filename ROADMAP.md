# Development Roadmap

## Current Status: ✅ MVP + Auth

MVP funcional com auth (JWT + refresh + GitHub OAuth), backend protegido e frontend com login.

---

## Phase 1: Testing & Validation ✅ (COMPLETED)

### Backend
- ✅ Unit tests for CNAB parser
- ✅ Unit tests for transaction models
- ✅ Service layer tests
- ✅ Controller tests

### Frontend
- ✅ Component structure
- ✅ Error handling
- ✅ Loading states

### Infrastructure
- ✅ Docker configuration
- ✅ Database initialization
- ✅ CORS setup

---

## Phase 2: Enhancement Features (Next Priority)

### API UX: Swagger & Docs 📖
- [ ] Enriquecer descrições e exemplos no Swagger
- [ ] API_DOCUMENTATION com requests/responses e códigos de erro

### Query & Escalabilidade 🔍
- [ ] Paginação e ordenação no GET por CPF
- [ ] Filtros (data, tipo) documentados
- [ ] Índices em CPF/Data/Tipo

### Qualidade & Observabilidade 🔐
- [ ] FluentValidation (CPF real) e respostas ProblemDetails
- [ ] Logging estruturado (Serilog) + correlação
- [ ] Telemetria (App Insights opcional)
- [ ] Versionamento de API (v1)

### Performance ⚡
- [ ] Caching (IMemoryCache) para consultas frequentes
- [ ] Otimizações de banco (índices adicionais, análise de planos)

### Advanced Filtering & Search 🔍
*(parte alinhada com Query & Escalabilidade acima; export permanece opcional)*
- [ ] Export transactions to CSV/Excel (opcional)

### File History & Management 📋
**Estimated**: 1 day
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
**Estimated**: 2 days
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

### High Priority (Next 2-4 weeks)
1. ✅ Authentication & Authorization (+5 points)
2. Advanced Filtering & Search
3. Automated Tests (increase coverage)

### Medium Priority (Next 1-2 months)
1. Dashboard & Analytics
2. File History & Management
3. Batch Processing
4. Logging & Monitoring


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

### Current Implementation
1. **Docs**: README/API docs incompletos; Swagger sem exemplos ricos
2. **Paginação/Filtros**: Ausentes nas consultas
3. **Caching/Performance**: Não há cache ou tuning de índices
4. **Erro/Validação**: Falta FluentValidation/ProblemDetails; CPF válido não checado
5. **Logging/Observabilidade**: Logs básicos, sem correlação/telemetria
6. **Versionamento**: API sem versão explícita

### To Address
- [ ] Add comprehensive error logging
- [ ] Implement pagination at API and UI level
- [ ] Add input validation middleware
- [ ] Create integration test suite
- [ ] Implement API versioning
- [ ] Add deprecation warnings for future changes

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
| Phase 2: Enhancement Features | 2-3 weeks | ⏳ Next |
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
