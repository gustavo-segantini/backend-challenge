@echo off
REM Script to reload Prometheus and Grafana configurations

echo Reloading Prometheus configuration...
curl -X POST http://localhost:9090/-/reload

echo.
echo Prometheus configuration reloaded
echo.
echo To reload Grafana dashboards:
echo    1. Access http://localhost:3001
echo    2. Go to Configuration -^> Provisioning -^> Dashboards
echo    3. Or restart Grafana: docker-compose restart grafana
echo.
echo Check Prometheus alerts:
echo    http://localhost:9090/alerts
echo.
echo Check Prometheus rules:
echo    http://localhost:9090/rules

pause

