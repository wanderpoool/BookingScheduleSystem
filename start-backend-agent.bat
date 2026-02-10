@echo off
REM ============================================================
REM  Launch Backend Agent — Claude Code
REM  Runs in: src/BookingScheduleSystem.Api/
REM  Picks up: CLAUDE.md (local) + root CLAUDE.md
REM ============================================================

echo ============================================================
echo  BACKEND AGENT — BookingScheduleSystem.Api
echo ============================================================
echo.
echo  Ownership:  src/BookingScheduleSystem.Api/
echo  Reads:      src/BookingScheduleSystem.Contracts/
echo  Off-limits: src/BookingScheduleSystem.Web/
echo.
echo  Starting Claude Code...
echo ============================================================
echo.

cd /d "%~dp0src\BookingScheduleSystem.Api"

REM Launch Claude Code in the API directory.
REM Claude Code auto-discovers CLAUDE.md in the current dir and parent dirs.
REM
REM Usage: Pass your task as an argument, or omit for interactive mode.
REM   Example: start-backend-agent.bat "Implement the upgrade subscription endpoint"
REM

if "%~1"=="" (
    claude
) else (
    claude "%~1"
)
