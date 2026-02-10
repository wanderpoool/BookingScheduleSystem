@echo off
REM ============================================================
REM  Launch Frontend Agent — Claude Code
REM  Runs in: src/BookingScheduleSystem.Web/
REM  Picks up: CLAUDE.md (local) + root CLAUDE.md
REM ============================================================

echo ============================================================
echo  FRONTEND AGENT — BookingScheduleSystem.Web
echo ============================================================
echo.
echo  Ownership:  src/BookingScheduleSystem.Web/
echo  Reads:      src/BookingScheduleSystem.Contracts/
echo  Off-limits: src/BookingScheduleSystem.Api/
echo.
echo  Starting Claude Code...
echo ============================================================
echo.

cd /d "%~dp0src\BookingScheduleSystem.Web"

REM Launch Claude Code in the Web directory.
REM Claude Code auto-discovers CLAUDE.md in the current dir and parent dirs.
REM
REM Usage: Pass your task as an argument, or omit for interactive mode.
REM   Example: start-frontend-agent.bat "Build the booking calendar component"
REM

if "%~1"=="" (
    claude
) else (
    claude "%~1"
)
