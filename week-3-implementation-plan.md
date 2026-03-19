# Week 3: Client-Manager Consciousness UI Integration

**Goal:** Integrate geometric intelligence service into client-manager to provide consciousness visualization and learning analytics.

**Context:** Week 2 delivered complete geometric reasoning service (44 tests passing, ~3,800 lines). Now integrate into production application.

---

## Architecture Overview

```
Client-Manager Frontend (React)
    ↓
Client-Manager API (ASP.NET Core)
    ↓
Hazina.Services.Geometric (Week 2 implementation)
    ↓
GeometricReasoningDbContext (SQL Server LocalDB)
```

---

## Week 3 Plan (5 Days)

### Monday: API Layer
**Goal:** Create REST API endpoints in client-manager

**Tasks:**
1. Add Hazina.Services.Geometric reference to ClientManagerAPI
2. Register services in DI (Program.cs)
3. Create GeometricController with endpoints:
   - `POST /api/geometric/thought-spaces` - Create thought space
   - `GET /api/geometric/thought-spaces/{userId}` - Get user's spaces
   - `POST /api/geometric/concepts` - Add concept
   - `POST /api/geometric/learning-events` - Record learning
   - `GET /api/geometric/analysis/{spaceId}` - Get learning analysis
   - `GET /api/geometric/learning-path/{spaceId}` - Optimal path
4. Add DTOs for request/response models
5. Add API tests (integration tests)

**Deliverables:**
- GeometricController.cs (~200 lines)
- DTOs (~100 lines)
- 6-8 API integration tests

---

### Tuesday: Database Integration
**Goal:** Connect geometric DB to client-manager infrastructure

**Tasks:**
1. Add GeometricReasoningDbContext to client-manager
2. Configure connection string (appsettings.json)
3. Run migrations on client-manager database
4. Seed initial data for existing users
5. Test database connectivity
6. Add health check for geometric DB

**Deliverables:**
- Database migration applied
- Seed script for existing users
- Health check endpoint

---

### Wednesday: Frontend Components
**Goal:** Build React UI for consciousness visualization

**Tasks:**
1. Create `ThoughtSpaceVisualization` component
   - Display thought space with concepts as nodes
   - Color-code by mastery level (red → green gradient)
   - Size nodes by local curvature (larger = more confusing)
2. Create `ConceptDetailPanel` component
   - Show mastery level, practice count, last practiced
   - Display learning events timeline
   - Show prerequisite relationships
3. Create `LearningAnalysisCard` component
   - Average mastery, global curvature, learning velocity
   - Struggling concepts list
   - Mastered concepts list
   - Recommended next concept
4. Create `PracticeSessionForm` component
   - Select concept
   - Choose event type
   - Enter duration
   - Submit and update UI

**Deliverables:**
- 4 React components (~400 lines)
- CSS styling
- Component tests (React Testing Library)

---

### Thursday: Visualization Library
**Goal:** 3D/2D visualization of thought space manifold

**Tasks:**
1. Evaluate libraries (D3.js, Three.js, Vis.js, React-Force-Graph)
2. Implement force-directed graph visualization
   - Nodes: Concepts (colored by mastery)
   - Edges: Prerequisites (directed arrows)
   - Physics simulation (attraction/repulsion)
3. Add interactive features:
   - Click concept → show details
   - Hover → show tooltip (mastery, curvature)
   - Drag nodes to reposition
   - Zoom/pan controls
4. Add curvature heat map overlay (optional)

**Deliverables:**
- ThoughtSpaceGraph component (~200 lines)
- Interactive visualization
- Performance optimization for large graphs

---

### Friday: Integration + Polish
**Goal:** End-to-end integration and UX refinement

**Tasks:**
1. Create main Consciousness page in client-manager
2. Wire up all components (form → API → update graph)
3. Add loading states, error handling, optimistic updates
4. Add real-time updates (SignalR if needed)
5. Polish UI/UX:
   - Smooth animations
   - Responsive layout
   - Accessibility (ARIA labels)
6. Create demo data set (sample thought space)
7. End-to-end testing
8. Documentation (screenshots, usage guide)

**Deliverables:**
- Complete consciousness UI page
- Demo data script
- End-to-end tests
- User documentation with screenshots

---

## Technical Stack

**Backend:**
- ASP.NET Core 9.0
- Hazina.Services.Geometric
- EF Core (SQL Server LocalDB)

**Frontend:**
- React 18+
- React-Force-Graph or D3.js
- TailwindCSS for styling
- React Query for API state

**Testing:**
- xUnit (backend)
- React Testing Library (frontend)
- Playwright (E2E)

---

## Success Criteria

Week 3 complete when:
- ✅ API endpoints functional (6-8 endpoints)
- ✅ Database integrated and seeded
- ✅ UI components built and tested
- ✅ Visualization working (interactive graph)
- ✅ End-to-end flow: Create space → Add concepts → Practice → See analysis
- ✅ Documentation complete
- ✅ All tests passing (backend + frontend)

---

## Risks & Mitigation

**Risk 1:** Visualization performance with large graphs (100+ concepts)
- **Mitigation:** Implement pagination, lazy loading, or graph simplification

**Risk 2:** Complex 3D visualization may be overkill
- **Mitigation:** Start with 2D force-directed graph, add 3D as enhancement

**Risk 3:** Real-time updates complexity
- **Mitigation:** Start with polling, add SignalR only if needed

**Risk 4:** Frontend/backend type mismatches
- **Mitigation:** Use TypeScript on frontend, generate types from C# DTOs

---

## Next Steps After Week 3

**Week 4:** Production readiness
- Performance optimization
- User testing
- Bug fixes
- Production deployment

**Week 5:** Advanced features
- Multi-domain thought spaces
- Cross-domain analogies
- Adaptive learning recommendations
- Export/import functionality

---

**Status:** Planning complete, ready to start Monday (API layer)
**Dependencies:** Week 2 complete ✅ (PR #205)
**Branch Strategy:** Create `feature/consciousness-ui-week3` from `develop`
