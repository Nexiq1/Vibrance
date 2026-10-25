@echo off
rem Builds dist\Vibrance.exe (single file, needs the .NET 8+ Desktop Runtime to run).
dotnet publish "%~dp0src\Vibrance.csproj" -c Release -o "%~dp0dist"
