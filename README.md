# UnboundDNSManager

A modern ASP.NET Core Blazor Server web application for remotely managing Unbound DNS servers. This application provides a user-friendly web interface to control and monitor your Unbound DNS server without needing command-line access.

![Platform](https://img.shields.io/badge/platform-Linux-FCC624?logo=linux&logoColor=black)

## Features

- **Dashboard**: Real-time server status and statistics
- **Cache Management**: Flush domains, record types, or entire zones
- **Local Zones**: Add, remove, and list local DNS zones
- **Local Records**: Manage DNS records (A, AAAA, CNAME, MX, TXT, PTR)
- **Forward Zones**: Configure DNS forwarding to upstream servers
- **Query Tool**: Lookup domains directly through the interface

## Prerequisites

- .NET 10.0 SDK or later
- Unbound DNS server (1.8.0 or later)
- Linux operating system (Ubuntu, Debian, CentOS, etc.)

## Installation

### 1. Clone the Repository

```bash
git clone https://github.com/DouglasFeaster/UnboundDNSManager.git
cd UnboundDNSManager
```

### 2. Configure Unbound

Edit your Unbound configuration file (`/etc/unbound/unbound.conf`):

#### For Plain TCP Connection (No SSL)

```conf
remote-control:
    control-enable: yes
    control-interface: 0.0.0.0
    control-port: 8953
    control-use-cert: no
```

#### For SSL/TLS Connection (Recommended)

```conf
remote-control:
    control-enable: yes
    control-interface: 0.0.0.0
    control-port: 8953
    control-use-cert: yes
    server-key-file: "/etc/unbound/unbound_server.key"
    server-cert-file: "/etc/unbound/unbound_server.pem"
    control-key-file: "/etc/unbound/unbound_control.key"
    control-cert-file: "/etc/unbound/unbound_control.pem"
```

Generate certificates (if using SSL):

```bash
sudo unbound-control-setup
```

Restart Unbound:

```bash
sudo systemctl restart unbound
```

### 3. Configure the Application

Edit `appsettings.json`:

```json
{
  "Unbound": {
    "Host": "127.0.0.1",
    "Port": 8953,
    "UseSSL": false,
    "ServerCertFile": "/etc/unbound/unbound_server.pem",
    "ControlCertFile": "/etc/unbound/unbound_control.pem",
    "ControlKeyFile": "/etc/unbound/unbound_control.key",
    "ConnectTimeoutMs": 5000
  }
}
```

**Note**: Set `UseSSL` to `true` if you configured Unbound with certificates.

### 4. Build and Run

```bash
dotnet restore
dotnet build
dotnet run
```

The application will start on `http://localhost:5000`

## Usage

### Dashboard

Access the dashboard at the root URL to view:
- Server status
- Reload configuration
- View statistics

### Cache Management

Navigate to **Cache Management** to:
- Flush specific domains: `example.com`
- Flush record types: `example.com` + `A`
- Flush entire zones: `example.com`

### Local Zones

Navigate to **Local Zones** to:
- Add zones with types: `static`, `deny`, `refuse`, `redirect`, `transparent`
- View all configured zones
- Remove zones

### Local Records

Navigate to **Local Records** to:
- Add DNS records: `www.example.local A 192.168.1.100`
- View all local records
- Remove records

### Forward Zones

Navigate to **Forward Zones** to:
- Add forward zones: `example.com` → `8.8.8.8`
- List all forwards
- Remove forwards