# 🚀 Digital Ocean Deployment Guide

## Mess Management System - Docker Deployment

This guide will help you deploy the Mess Management System to Digital Ocean using Docker.

---

## 📋 Prerequisites

1. A Digital Ocean account
2. A Droplet running Ubuntu 22.04 or 24.04
3. Docker and Docker Compose installed on the Droplet
4. Git (to clone your repository)

---

## 🏗️ Files Created for Deployment

| File | Purpose |
|------|---------|
| `Dockerfile` | Multi-stage build for .NET 8 app |
| `docker-compose.yml` | Easy container orchestration |
| `appsettings.Production.json` | Production config with SQLite |
| `.dockerignore` | Optimize Docker build |

---

## 🔧 Step 1: Set Up Your Digital Ocean Droplet

### Option A: Create a New Droplet

1. Go to [Digital Ocean](https://cloud.digitalocean.com/)
2. Create a new Droplet:
   - **Image**: Ubuntu 22.04 (LTS)
   - **Plan**: Basic ($6/month minimum, $12/month recommended)
   - **Region**: Choose closest to your users
   - **Authentication**: SSH Keys (recommended)

### Option B: Use Docker Droplet (Recommended)

1. In Digital Ocean Marketplace, search for "Docker"
2. Select the Docker image (comes with Docker pre-installed)
3. Create Droplet with this image

---

## 🐳 Step 2: Install Docker (if not pre-installed)

SSH into your droplet and run:

```bash
# Update packages
sudo apt update && sudo apt upgrade -y

# Install Docker
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh

# Install Docker Compose
sudo apt install docker-compose-plugin -y

# Add your user to docker group
sudo usermod -aG docker $USER

# Logout and login again for changes to take effect
exit
```

---

## 📦 Step 3: Deploy the Application

### Clone Your Repository

```bash
# Clone the repository
git clone https://github.com/YOUR_USERNAME/YOUR_REPO.git
cd YOUR_REPO/EAD-Arham
```

### Or Upload Files via SCP

```bash
# From your local machine
scp -r ./EAD-Arham root@YOUR_DROPLET_IP:/root/app
```

### Build and Run

```bash
# Navigate to project directory
cd /root/app

# Build and start the container
docker compose up -d --build

# View logs
docker compose logs -f

# Check if container is running
docker ps
```

---

## 🌐 Step 4: Configure Firewall

```bash
# Allow HTTP traffic
sudo ufw allow 80/tcp

# Allow HTTPS traffic (for future SSL)
sudo ufw allow 443/tcp

# Enable firewall
sudo ufw enable
```

---

## 🔒 Step 5: Set Up SSL with Nginx (Optional but Recommended)

### Install Nginx and Certbot

```bash
sudo apt install nginx certbot python3-certbot-nginx -y
```

### Create Nginx Configuration

```bash
sudo nano /etc/nginx/sites-available/mess-management
```

Add this configuration:

```nginx
server {
    listen 80;
    server_name YOUR_DOMAIN.com www.YOUR_DOMAIN.com;

    location / {
        proxy_pass http://localhost:80;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

### Enable the Site

```bash
sudo ln -s /etc/nginx/sites-available/mess-management /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl restart nginx
```

### Get SSL Certificate

```bash
sudo certbot --nginx -d YOUR_DOMAIN.com -d www.YOUR_DOMAIN.com
```

---

## 📝 Port Configuration

| Service | Internal Port | External Port | Description |
|---------|--------------|---------------|-------------|
| ASP.NET Core | 8080 | 80 | Main application |
| SQLite | N/A | N/A | File-based database |

---

## 👤 Default Login Credentials

### Admin Account
- **Username**: `admin`
- **Password**: `admin123`

### Teacher Accounts (15 Pakistani teachers seeded)
- **Username**: `ahmed_khan`, `fatima_malik`, `ali_qureshi`, etc.
- **Password**: `teacher123`
- **Note**: Teachers must change password on first login

---

## 🗄️ Database Information

- **Type**: SQLite
- **Location**: `/app/data/MessManagementDB.db` (inside container)
- **Persistence**: Data volume `mess-data`

### Backup Database

```bash
# Copy database from container
docker cp mess-management-system:/app/data/MessManagementDB.db ./backup_$(date +%Y%m%d).db
```

### Restore Database

```bash
# Copy database to container
docker cp ./backup.db mess-management-system:/app/data/MessManagementDB.db
docker compose restart
```

---

## 🔄 Common Commands

```bash
# View container logs
docker compose logs -f

# Restart the application
docker compose restart

# Stop the application
docker compose down

# Start the application
docker compose up -d

# Rebuild after code changes
docker compose up -d --build

# Check container health
docker ps

# Enter container shell
docker exec -it mess-management-system bash
```

---

## ⚠️ Troubleshooting

### Container won't start
```bash
docker compose logs
# Check for error messages
```

### Port already in use
```bash
sudo lsof -i :80
# Kill the process using the port or change the port in docker-compose.yml
```

### Database connection issues
```bash
# Check if data directory exists
docker exec -it mess-management-system ls -la /app/data/

# Check database file permissions
docker exec -it mess-management-system chmod 777 /app/data/MessManagementDB.db
```

---

## 🎉 Success!

Your Mess Management System should now be accessible at:
- **HTTP**: `http://YOUR_DROPLET_IP`
- **HTTPS**: `https://YOUR_DOMAIN.com` (if SSL configured)

---

## 📞 Support

For issues, check:
1. Docker logs: `docker compose logs`
2. Application logs inside container
3. Nginx logs: `sudo tail -f /var/log/nginx/error.log`

