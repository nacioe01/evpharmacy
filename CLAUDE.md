Pharmacy POS – Project Instructions
Project Overview

A desktop Pharmacy Point-of-Sale (POS) application designed for a single-computer pharmacy operation.

Technology Stack
IDE: Visual Studio 2026
Programming Language: C#
Framework: WPF
Database: SQLite
Architecture: Preserve the existing project architecture where possible.
UI Design: Modern, professional pharmacy POS interface.
Existing Features
Login page
Temporary dashboard
Existing SQLite database file
Planned Modules
Dashboard
Medicine Management
Category Management
Supplier Management
Inventory Management
Batch and Expiration Tracking
Stock Receiving
Stock Adjustments
Point of Sale
Barcode Scanning
Sales and Payment Management
Receipt Printing
Reports and Analytics
User Management
Audit Logs
Database Backup and Restoration
Philippine VAT, Senior Citizen, and PWD Discount Support
Development Rules
Always inspect existing code before making changes.
Never recreate the project from scratch.
Preserve existing working functionality.
Follow the existing naming conventions unless improvements are approved.
Use clean, maintainable, reusable C# code.
Follow WPF and MVVM best practices where appropriate.
Separate UI, business logic, and database operations.
Use parameterized SQLite queries.
Enforce SQLite foreign keys.
Use database transactions for sales and inventory operations.
Store monetary values using integer centavos where appropriate.
Never hardcode passwords or sensitive credentials.
Implement appropriate error handling and input validation.
Avoid unnecessary NuGet packages.
Never permanently delete historical sales or inventory records.
Pharmacy Business Requirements
Medicine Management
Generic name
Brand name
Barcode
Category
Dosage form
Strength
Unit of measurement
Purchase cost
Selling price
Reorder level
Prescription classification
Inventory Management
Batch or lot number tracking
Expiration date tracking
Stock receiving
Stock adjustments
Stock movement history
Low-stock alerts
Expiration alerts
First-Expiry-First-Out (FEFO) stock selection
Prevent sales of expired or recalled stock
POS Requirements
Barcode scanning
Manual product search
Shopping cart
Quantity management
Discounts
Payment processing
Receipt printing
Transaction history
Philippine Pharmacy Requirements
VAT handling
Senior Citizen discount eligibility
PWD discount eligibility
Appropriate discount documentation
Configurable tax and discount rules
Proper transaction records

All tax and discount calculations must follow applicable Philippine regulations. Do not assume every medicine or transaction qualifies for an exemption.

Database Rules
Keep product master records separate from inventory batches.
Maintain historical stock movements.
Use foreign key relationships.
Use database transactions for related updates.
Do not silently overwrite historical records.
Back up the database before structural changes.
Never delete or recreate the existing database without approval.
UI/UX Requirements
Modern professional pharmacy POS appearance.
Clean and consistent WPF styling.
Reusable controls and styles.
Clear navigation.
Readable DataGrids.
Consistent spacing and typography.
User-friendly validation messages.
Avoid unnecessary visual clutter.
Claude Code Working Rules

Before implementing any feature:

Inspect the related existing files.
Explain the proposed implementation.
Identify files that will be created or modified.
Explain database changes if applicable.
Wait for approval before major changes.

After implementation:

Build the project.
Fix compilation errors.
Test affected functionality where possible.
Explain the changes made.
Identify any remaining issues.
Current Development Priority

FIRST: Inspect the existing project.

SECOND: Review the login page and temporary dashboard.

THIRD: Review the SQLite database and existing architecture.

FOURTH: Recommend the database and application architecture for medicine and inventory management.

FIFTH: Prepare a phased implementation plan.

IMPORTANT: Do not modify, delete, or rewrite any files during the initial project assessment. Wait for explicit approval before implementing changes.

Overall Objective

Develop a reliable, maintainable, professional Pharmacy POS system that preserves existing functionality and supports accurate medicine inventory, batch tracking, expiration monitoring, barcode scanning, sales processing, and Philippine pharmacy business requirements.