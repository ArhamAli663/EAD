-- Mess Management System Database Queries
-- Connection: (localdb)\mssqllocaldb | Database: MessManagementDB

-- View all users
SELECT * FROM Users;

-- View all teachers
SELECT * FROM Teachers;

-- View all menu items
SELECT * FROM MenuItems;

-- View all attendances
SELECT * FROM Attendances;

-- View all bills
SELECT * FROM Bills;

-- View all attendance disputes
SELECT * FROM AttendanceDisputes;

-- DELETE specific dispute (Dispute #11 for Arham Ali - lunch issue)
-- Run this to remove the problematic dispute
DELETE FROM AttendanceDisputes WHERE DisputeId = 11;

-- ========================================
-- INSERT WEEKLY MENU PLAN WITH RATES
-- ========================================
-- Clear existing menu items (optional)
-- DELETE FROM MenuItems;

-- ========================================
-- UPDATE EXISTING MENU TO PAKISTANI DISHES
-- (Delete old items and add new Pakistani menu)
-- ========================================

-- First, delete all existing menu items
DELETE FROM MenuItems;

-- Reset identity seed
DBCC CHECKIDENT ('MenuItems', RESEED, 0);

-- Monday Menu - Pakistani Dishes (Only Lunch & Dinner)
INSERT INTO MenuItems (ItemName, Description, MealType, DayOfWeek, RatePerServing, IsActive, CreatedDate) 
VALUES 
('Chicken Karahi with Naan', 'Traditional Pakistani chicken karahi with fresh naan', 'Lunch', 'Monday', 85.00, 1, GETDATE()),
('Nihari with Roti', 'Slow-cooked beef stew with traditional roti', 'Dinner', 'Monday', 95.00, 1, GETDATE());

-- Tuesday Menu - Pakistani Dishes (Only Lunch & Dinner)
INSERT INTO MenuItems (ItemName, Description, MealType, DayOfWeek, RatePerServing, IsActive, CreatedDate)
VALUES 
('Biryani with Raita', 'Aromatic Pakistani biryani with cooling raita', 'Lunch', 'Tuesday', 90.00, 1, GETDATE()),
('Haleem with Naan', 'Slow-cooked wheat and meat porridge with naan', 'Dinner', 'Tuesday', 85.00, 1, GETDATE());

-- Wednesday Menu - Pakistani Dishes (Only Lunch & Dinner)
INSERT INTO MenuItems (ItemName, Description, MealType, DayOfWeek, RatePerServing, IsActive, CreatedDate)
VALUES 
('Chapli Kebab with Rice', 'Spicy minced meat patties with steamed rice', 'Lunch', 'Wednesday', 80.00, 1, GETDATE()),
('Paya with Roti', 'Traditional trotters curry with roti', 'Dinner', 'Wednesday', 90.00, 1, GETDATE());

-- Thursday Menu - Pakistani Dishes (Only Lunch & Dinner)
INSERT INTO MenuItems (ItemName, Description, MealType, DayOfWeek, RatePerServing, IsActive, CreatedDate)
VALUES 
('Aloo Gosht with Naan', 'Potato and mutton curry with naan', 'Lunch', 'Thursday', 85.00, 1, GETDATE()),
('Seekh Kebab with Paratha', 'Grilled minced meat skewers with paratha', 'Dinner', 'Thursday', 90.00, 1, GETDATE());

-- Friday Menu - Pakistani Dishes (Only Lunch & Dinner)
INSERT INTO MenuItems (ItemName, Description, MealType, DayOfWeek, RatePerServing, IsActive, CreatedDate)
VALUES 
('Pulao with Chicken Korma', 'Fragrant rice with creamy chicken korma', 'Lunch', 'Friday', 95.00, 1, GETDATE()),
('Karahi Gosht with Roti', 'Mutton karahi cooked in wok with roti', 'Dinner', 'Friday', 100.00, 1, GETDATE());

-- Saturday Menu - Pakistani Dishes (Only Lunch & Dinner)
INSERT INTO MenuItems (ItemName, Description, MealType, DayOfWeek, RatePerServing, IsActive, CreatedDate)
VALUES 
('Dal Makhani with Naan', 'Creamy black lentils with butter naan', 'Lunch', 'Saturday', 75.00, 1, GETDATE()),
('Chicken Jalfrezi with Rice', 'Stir-fried chicken with vegetables and rice', 'Dinner', 'Saturday', 85.00, 1, GETDATE());

-- Sunday Menu - Pakistani Special (Only Lunch & Dinner)
INSERT INTO MenuItems (ItemName, Description, MealType, DayOfWeek, RatePerServing, IsActive, CreatedDate)
VALUES 
('Special Sindhi Biryani', 'Authentic Sindhi biryani with spicy potatoes', 'Lunch', 'Sunday', 110.00, 1, GETDATE()),
('Sajji with Naan', 'Whole roasted chicken with special rice and naan', 'Dinner', 'Sunday', 120.00, 1, GETDATE());

-- ========================================
-- CONFIGURE BILLING SYSTEM
-- ========================================
-- Clear existing billing configuration (optional)
-- DELETE FROM BillingConfigurations;

-- Insert billing configuration
INSERT INTO BillingConfigurations (MonthlyWaterBillTotal, DefaultBreakfastRate, DefaultLunchRate, DefaultDinnerRate, LastUpdated, UpdatedBy)
VALUES 
(5000.00, 30.00, 65.00, 80.00, GETDATE(), 1);

-- Verify menu items inserted
SELECT COUNT(*) AS TotalMenuItems FROM MenuItems WHERE IsActive = 1;

-- Verify billing configuration
SELECT * FROM BillingConfigurations;

-- ========================================
-- RESET IDENTITY SEEDS (Use with caution!)
-- ========================================
-- Reset Teachers table ID to 1 (only use when table is empty)
-- DELETE FROM Teachers; DBCC CHECKIDENT ('Teachers', RESEED, 0);

-- Reset Users table ID to 1 (only use when table is empty)
-- DELETE FROM Users; DBCC CHECKIDENT ('Users', RESEED, 0);

-- Reset Attendances table ID to 1 (only use when table is empty)
-- DELETE FROM Attendances; DBCC CHECKIDENT ('Attendances', RESEED, 0);

-- Check current identity values
SELECT 
    OBJECT_NAME(object_id) AS TableName,
    IDENT_CURRENT(OBJECT_NAME(object_id)) AS CurrentIdentity
FROM sys.identity_columns
WHERE OBJECT_SCHEMA_NAME(object_id) = 'dbo';

-- Teacher details with user info
SELECT 
    t.TeacherId,
    t.FullName,
    t.Email,
    t.PhoneNumber,
    t.Department,
    t.JoiningDate,
    u.Username,
    u.Role,
    t.IsActive
FROM Teachers t
INNER JOIN Users u ON t.UserId = u.UserId;

-- Attendance summary by teacher
SELECT 
    t.FullName,
    COUNT(*) as TotalDays,
    SUM(CAST(a.BreakfastTaken as INT)) as BreakfastCount,
    SUM(CAST(a.LunchTaken as INT)) as LunchCount,
    SUM(CAST(a.DinnerTaken as INT)) as DinnerCount
FROM Attendances a
INNER JOIN Teachers t ON a.TeacherId = t.TeacherId
GROUP BY t.FullName;

-- Weekly menu overview
SELECT 
    DayOfWeek,
    MAX(CASE WHEN MealType = 'Breakfast' THEN ItemName END) as Breakfast,
    MAX(CASE WHEN MealType = 'Lunch' THEN ItemName END) as Lunch,
    MAX(CASE WHEN MealType = 'Dinner' THEN ItemName END) as Dinner
FROM MenuItems
WHERE IsActive = 1
GROUP BY DayOfWeek
ORDER BY 
    CASE DayOfWeek
        WHEN 'Monday' THEN 1
        WHEN 'Tuesday' THEN 2
        WHEN 'Wednesday' THEN 3
        WHEN 'Thursday' THEN 4
        WHEN 'Friday' THEN 5
        WHEN 'Saturday' THEN 6
        WHEN 'Sunday' THEN 7
    END;
