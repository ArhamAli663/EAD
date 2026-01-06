# 🇵🇰 Mess Management System - Seed Data

## Overview

This document describes all the data that is automatically seeded into the database when the application starts for the first time.

---

## 👤 Admin Account

| Field | Value |
|-------|-------|
| Username | `admin` |
| Password | `admin123` |
| Role | Admin |

---

## 👥 Pakistani Teachers (15)

All teachers have the default password: `teacher123`

| # | Full Name | Username | Email | Phone | Department |
|---|-----------|----------|-------|-------|------------|
| 1 | Muhammad Ahmed Khan | `ahmed_khan` | ahmed.khan@mess.edu.pk | 0300-1234567 | Computer Science |
| 2 | Fatima Zahra Malik | `fatima_malik` | fatima.malik@mess.edu.pk | 0321-2345678 | Mathematics |
| 3 | Ali Hassan Qureshi | `ali_qureshi` | ali.qureshi@mess.edu.pk | 0333-3456789 | Physics |
| 4 | Ayesha Siddiqui | `ayesha_siddiqui` | ayesha.siddiqui@mess.edu.pk | 0345-4567890 | Chemistry |
| 5 | Usman Tariq | `usman_tariq` | usman.tariq@mess.edu.pk | 0312-5678901 | English Literature |
| 6 | Zainab Bibi | `zainab_bibi` | zainab.bibi@mess.edu.pk | 0301-6789012 | Urdu |
| 7 | Imran Hussain Shah | `imran_shah` | imran.shah@mess.edu.pk | 0322-7890123 | History |
| 8 | Sana Noor | `sana_noor` | sana.noor@mess.edu.pk | 0334-8901234 | Biology |
| 9 | Bilal Ahmed Rana | `bilal_rana` | bilal.rana@mess.edu.pk | 0346-9012345 | Economics |
| 10 | Maryam Khalid | `maryam_khalid` | maryam.khalid@mess.edu.pk | 0313-0123456 | Psychology |
| 11 | Hassan Raza Bukhari | `hassan_bukhari` | hassan.bukhari@mess.edu.pk | 0302-1234568 | Political Science |
| 12 | Amna Parveen | `amna_parveen` | amna.parveen@mess.edu.pk | 0323-2345679 | Sociology |
| 13 | Farhan Ali Chaudhry | `farhan_chaudhry` | farhan.chaudhry@mess.edu.pk | 0335-3456780 | Business Administration |
| 14 | Hira Batool | `hira_batool` | hira.batool@mess.edu.pk | 0347-4567891 | Fine Arts |
| 15 | Asad Mehmood Bhatti | `asad_bhatti` | asad.bhatti@mess.edu.pk | 0314-5678902 | Physical Education |

---

## 🍽️ Pakistani Menu Items (21 items - 7 days × 3 meals)

### Monday
| Meal | Item | Description | Rate (PKR) |
|------|------|-------------|------------|
| Breakfast | Halwa Puri | Traditional halwa with crispy puris | 80 |
| Lunch | Chicken Biryani | Fragrant rice with spiced chicken | 150 |
| Dinner | Daal Chawal | Lentils with steamed rice | 100 |

### Tuesday
| Meal | Item | Description | Rate (PKR) |
|------|------|-------------|------------|
| Breakfast | Paratha with Omelette | Flaky paratha with egg omelette | 70 |
| Lunch | Nihari | Slow-cooked beef stew with naan | 180 |
| Dinner | Karahi Chicken | Spicy chicken in wok-style curry | 160 |

### Wednesday
| Meal | Item | Description | Rate (PKR) |
|------|------|-------------|------------|
| Breakfast | Chana Chaat | Chickpea salad with spices | 60 |
| Lunch | Mutton Pulao | Aromatic rice with tender mutton | 170 |
| Dinner | Aloo Gosht | Potato and meat curry | 140 |

### Thursday
| Meal | Item | Description | Rate (PKR) |
|------|------|-------------|------------|
| Breakfast | Aloo Paratha | Stuffed flatbread with spiced potatoes | 75 |
| Lunch | Fish Curry | Spicy fish in tomato gravy | 160 |
| Dinner | Palak Paneer | Spinach with cottage cheese | 120 |

### Friday
| Meal | Item | Description | Rate (PKR) |
|------|------|-------------|------------|
| Breakfast | Paya | Traditional trotters soup | 90 |
| Lunch | Beef Pulao | Fragrant rice with beef | 165 |
| Dinner | Chicken Korma | Creamy chicken curry | 150 |

### Saturday
| Meal | Item | Description | Rate (PKR) |
|------|------|-------------|------------|
| Breakfast | Nihari | Spicy slow-cooked beef with naan | 120 |
| Lunch | Kabuli Pulao | Afghan-style rice with meat and carrots | 175 |
| Dinner | Mix Vegetable | Seasonal vegetables curry | 110 |

### Sunday
| Meal | Item | Description | Rate (PKR) |
|------|------|-------------|------------|
| Breakfast | Haleem | Rich meat and lentil porridge | 100 |
| Lunch | Chicken Karahi | Wok-style chicken with tomatoes | 160 |
| Dinner | Daal Mash | Urad lentils with spices | 95 |

---

## 💰 Billing Configuration

| Setting | Value |
|---------|-------|
| Monthly Water Bill Total | PKR 5,000 |
| Default Breakfast Rate | PKR 30 |
| Default Lunch Rate | PKR 60 |
| Default Dinner Rate | PKR 50 |

---

## 📊 Sample Attendance Data

When teachers exist in the database, sample attendance records are generated for the past **10 days** with:
- ~85% probability of taking each meal (Breakfast, Lunch, Dinner)
- Recorded by Admin user
- Remarks auto-generated with date

---

## 🗄️ Database Information

| Property | Value |
|----------|-------|
| Database Type | SQLite |
| Database File | `MessManagementDB.db` |
| Auto-Created | Yes (on first startup) |
| Auto-Seeded | Yes (if tables are empty) |

---

## 🔐 Login Credentials Summary

### Admin
```
Username: admin
Password: admin123
```

### Teachers (example)
```
Username: ahmed_khan
Password: teacher123
```

> **Note:** Teachers are required to change their password on first login.

---

## 📝 Notes

1. All data is seeded only if the respective tables are empty
2. Passwords are hashed using BCrypt
3. Each teacher gets a linked User account automatically
4. Attendance data is only seeded if teachers exist
5. The database is created automatically on first run

