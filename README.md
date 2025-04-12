# COTA - Solana Tax Calculator

**COTA** is a Solana-based tax calculation application designed to help users calculate capital gains and staking rewards from their cryptocurrency transactions. It integrates with the Helius API to fetch transaction data and generates downloadable CSV reports for tax reporting purposes.

Built with **ASP.NET Core** for the backend API and **Blazor WebAssembly** for the frontend, COTA provides a modern, efficient, and user-friendly solution for Solana users to manage their tax obligations.

---

## Features
- **Transaction Data Fetching**: Retrieves transaction history from the Solana blockchain via the Helius API.
- **Tax Calculation**: 
  - Uses the **FIFO (First-In, First-Out)** method to calculate capital gains.
  - Treats staking rewards as taxable income.
- **CSV Report Generation**: Generates downloadable CSV files for capital gains and staking income, suitable for tax reporting.
- **Modular Architecture**: Separates concerns between the API, client, and core logic for better maintainability.

---

## Prerequisites
Before setting up the project, ensure you have the following:
- **.NET SDK 8.0 or later**: [Download .NET SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Helius API Key**: Sign up at [Helius](https://www.helius.xyz/) to obtain a free API key.
- **Git**: To clone the repository.
- **Visual Studio 2022** (recommended) or another IDE like VS Code.

---

## Setup Instructions

### 1. Clone the Repository
```bash
git clone https://github.com/VBSquirrel/COTA.git
cd COTA
