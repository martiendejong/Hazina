# Hazina Grafana Dashboards

This directory contains pre-built Grafana dashboard configurations for monitoring Hazina AI applications.

## Available Dashboards

### 1. Hazina AI - Overview (`hazina-overview-dashboard.json`)

A comprehensive dashboard showing:

**Operations Monitoring:**
- Operations rate (ops/sec) by provider and success status
- Overall success rate gauge
- Operation latency (p95/p99) by provider

**Provider Health:**
- Provider health scores
- Provider failover events

**Cost & Usage:**
- Cost per hour by provider
- Token usage rate (input/output)

**Quality Metrics:**
- Hallucinations detected by type
- Fault detection and correction

**NeuroChain:**
- Layer usage by complexity

## Installation

### Quick Import

1. Open Grafana UI
2. Navigate to **Dashboards** → **Import**
3. Upload `hazina-overview-dashboard.json`
4. Select your Prometheus data source
5. Click **Import**

### Using Grafana API

```bash
# Set your Grafana details
GRAFANA_URL="http://localhost:3000"
GRAFANA_API_KEY="your-api-key"

# Import dashboard
curl -X POST "$GRAFANA_URL/api/dashboards/db" \
  -H "Authorization: Bearer $GRAFANA_API_KEY" \
  -H "Content-Type: application/json" \
  -d @hazina-overview-dashboard.json
```

### Using Grafana Provisioning

1. Copy dashboard files to Grafana provisioning directory:
```bash
cp *.json /etc/grafana/provisioning/dashboards/
```

2. Create provisioning config `/etc/grafana/provisioning/dashboards/hazina.yaml`:
```yaml
apiVersion: 1

providers:
  - name: 'Hazina Dashboards'
    orgId: 1
    folder: 'Hazina'
    type: file
    disableDeletion: false
    updateIntervalSeconds: 10
    allowUiUpdates: true
    options:
      path: /etc/grafana/provisioning/dashboards
      foldersFromFilesStructure: true
```

3. Restart Grafana

## Configuration

### Data Source

All dashboards expect a Prometheus data source. Make sure your Prometheus is scraping the Hazina `/metrics` endpoint.

Example Prometheus scrape config:
```yaml
scrape_configs:
  - job_name: 'hazina-app'
    static_configs:
      - targets: ['localhost:8080']
    metrics_path: '/metrics'
    scrape_interval: 15s
```

### Alerts

You can add alerts to any panel. Recommended alerts:

1. **High Error Rate**
   - Metric: `sum(rate(hazina_operations_total{success="false"}[5m])) / sum(rate(hazina_operations_total[5m])) > 0.05`
   - Threshold: > 5%

2. **High Latency**
   - Metric: `histogram_quantile(0.95, sum(rate(hazina_operation_duration_ms_bucket[5m])) by (le))`
   - Threshold: > 5000ms

3. **Provider Health Degraded**
   - Metric: `hazina_provider_health < 0.5`
   - Threshold: < 0.5

4. **High Cost**
   - Metric: `increase(hazina_cost_usd_total[1h]) > 10`
   - Threshold: > $10/hour

5. **Hallucinations Detected**
   - Metric: `rate(hazina_hallucinations_detected_total[5m]) > 0.1`
   - Threshold: > 0.1/sec

## Customization

### Adding Variables

You can add template variables to filter by:
- Provider
- Operation type
- Environment
- Namespace (for Kubernetes)

Example variable configuration:
```json
{
  "name": "provider",
  "type": "query",
  "datasource": "Prometheus",
  "query": "label_values(hazina_operations_total, provider)",
  "multi": true,
  "includeAll": true
}
```

### Custom Panels

To add custom panels:
1. Edit the dashboard in Grafana UI
2. Add panel
3. Configure query and visualization
4. Export dashboard JSON
5. Replace the file in this directory

## Troubleshooting

**No data showing:**
- Verify Prometheus is scraping metrics: `http://your-app:8080/metrics`
- Check Prometheus targets: `http://prometheus:9090/targets`
- Verify data source in Grafana is configured correctly

**Missing metrics:**
- Ensure `AddHazinaObservability()` is called in your app
- Verify Hazina operations are actually running
- Check metric names in Prometheus: `http://prometheus:9090/graph`

**Dashboard not importing:**
- Check JSON syntax is valid
- Ensure Grafana version compatibility (tested with 8.0+)
- Try importing via UI instead of API

## Best Practices

1. **Set appropriate refresh intervals** - 30s is good for production, 5s for debugging
2. **Use time range selectors** - Monitor last 1h for real-time, last 24h for trends
3. **Create alerts** - Don't just monitor, get notified of issues
4. **Customize for your use case** - Clone and modify dashboards for specific needs
5. **Use annotations** - Mark deployments and incidents on dashboards

## Next Steps

- Set up Prometheus alerting rules
- Configure notification channels (Slack, email, PagerDuty)
- Create team-specific dashboards
- Add SLO tracking panels
- Integrate with incident management tools
