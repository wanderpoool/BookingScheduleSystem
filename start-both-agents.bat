@echo off
REM ============================================================
REM  Launch BOTH Agents in parallel — Two terminal windows
REM ============================================================

echo Starting Backend Agent...
start "Backend Agent" cmd /k "%~dp0start-backend-agent.bat"

echo Starting Frontend Agent...
start "Frontend Agent" cmd /k "%~dp0start-frontend-agent.bat"

echo.
echo Both agents launched in separate windows.
echo.
echo REMEMBER:
echo   - Give each agent the SAME feature/task to work on
echo   - Backend implements the API endpoint
echo   - Frontend implements the UI for it
echo   - They share contracts via src/BookingScheduleSystem.Contracts/
echo.
