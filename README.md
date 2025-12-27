# STPTunnel

Desktop application for managing SSH Tunnels via Jump Host  
Built with .NET 8 + Avalonia UI

---

**STPTunnel** คือแอปพลิเคชัน Desktop สำหรับจัดการ **SSH Tunnel ผ่าน Jump Host**  
ออกแบบมาเพื่อลดความยุ่งยากจากการใช้คำสั่ง SSH หรือ shell script ยาว ๆ  
ผู้ใช้สามารถควบคุมการเชื่อมต่อทั้งหมดได้ผ่าน **Tray Menu และหน้าจอ Settings**

เหมาะสำหรับงานพัฒนา, UAT, DevOps และระบบที่ต้อง forward port หลายจุดเป็นประจำ

---

### ✨ คุณสมบัติ (Features)

- เชื่อมต่อ SSH Tunnel ผ่าน Jump Host เพียงจุดเดียว
- รองรับหลาย Tunnel ต่อหนึ่ง Profile (เช่น UAT / PROD)
- เปิด–ปิดการเชื่อมต่อผ่าน **Tray Menu**
- แสดงสถานะการเชื่อมต่อ (Connected / Disconnected)
- สร้าง SSH Key (ed25519) ผ่าน UI
- ติดตั้ง Public Key ไปยัง Jump Host ได้
- เก็บการตั้งค่าเป็นไฟล์ JSON
- รองรับ macOS และ Windows

---

### Architecture
- UI Layer: Avalonia UI (XAML, Tray App)
- MVVM: SettingsVm
- Core: TunnelEngine, ConfigStore (JSON), SshKeyHelper
- System: OpenSSH (ssh / ssh-keygen)
     
---

### 🧭 Workflow การใช้งาน

1. เปิด **Settings**
2. กรอกข้อมูล Jump Host (Host / Port / User)
3. เพิ่ม Tunnel (LocalPort → RemoteHost:RemotePort)
4. กด **Save**
5. กด **Connect** จาก Tray Menu
6. ตรวจสอบสถานะว่า Connected แล้ว

---

### 📂 ตำแหน่งไฟล์ Config

- **macOS**
- **Windows**

---

### ⚠️ หมายเหตุเรื่อง Password ครั้งแรก

- การติดตั้ง SSH Key ครั้งแรก อาจต้องกรอก password ของ Jump Host
- ระบบจะเรียก SSH ผ่าน terminal / cmd ของระบบ
- หลังจากติดตั้ง key แล้ว จะไม่ต้องกรอก password อีก

---

### 🛠 Build & Run (สำหรับนักพัฒนา)
dotnet run

Publish (macOS Apple Silicon)
dotnet publish -c Release -r osx-arm64 --self-contained true /p:PublishSingleFile=true

Publish (Windows)
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true

---

### 📄 License
Personal / Internal use

---

### 👤 Author
Sitthiphong Krobkrong


