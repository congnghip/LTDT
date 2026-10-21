import 'dart:async';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../models/app_models.dart';
import '../services/auth_service.dart';
import '../services/chamcong_service.dart';
import 'login_screen.dart';

class AdminDashboardScreen extends StatefulWidget {
  const AdminDashboardScreen({super.key});

  @override
  State<AdminDashboardScreen> createState() => _AdminDashboardScreenState();
}

class _AdminDashboardScreenState extends State<AdminDashboardScreen> with SingleTickerProviderStateMixin {
  final ChamCongService _service = ChamCongService();
  late TabController _tabController;

  List<AttendanceModel> _allAttendances = [];
  List<UserModel> _employees = [];
  List<ShiftModel> _shifts = [];
  List<LeaveRequestModel> _allLeaveRequests = [];

  bool _isLoading = false;
  final currencyFormat = NumberFormat("#,##0", "vi_VN");
  final timeFormat = DateFormat("HH:mm");
  final dateFormat = DateFormat("dd/MM/yyyy");

  // Dynamic QR
  String? _dynamicQrToken;
  int _qrCountdown = 15;
  Timer? _qrTimer;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 5, vsync: this);
    _loadAllData();
    _startDynamicQrLoop();
  }

  @override
  void dispose() {
    _qrTimer?.cancel();
    super.dispose();
  }

  void _startDynamicQrLoop() {
    _refreshQr();
    _qrTimer = Timer.periodic(const Duration(seconds: 1), (timer) {
      if (!mounted) return;
      setState(() {
        if (_qrCountdown > 1) {
          _qrCountdown--;
        } else {
          _qrCountdown = 15;
          _refreshQr();
        }
      });
    });
  }

  Future<void> _refreshQr() async {
    final qrData = await _service.getDynamicQr();
    if (qrData != null && mounted) {
      setState(() {
        _dynamicQrToken = qrData['token'];
        _qrCountdown = qrData['expiresInSeconds'] ?? 15;
      });
    }
  }

  Future<void> _loadAllData() async {
    setState(() => _isLoading = true);
    await Future.wait([
      _loadAttendances(),
      _loadEmployees(),
      _loadShifts(),
      _loadLeaveRequests(),
    ]);
    setState(() => _isLoading = false);
  }

  Future<void> _loadAttendances() async {
    final list = await _service.getAllAttendances();
    setState(() => _allAttendances = list);
  }

  Future<void> _loadEmployees() async {
    final list = await _service.getEmployees();
    setState(() => _employees = list);
  }

  Future<void> _loadShifts() async {
    final list = await _service.getShifts();
    setState(() => _shifts = list);
  }

  Future<void> _loadLeaveRequests() async {
    final list = await _service.getAllLeaveRequests();
    setState(() => _allLeaveRequests = list);
  }

  // === CRUD NHÂN VIÊN ===
  Future<void> _showAddEmployeeDialog() async {
    final usernameCtrl = TextEditingController();
    final nameCtrl = TextEditingController();
    final emailCtrl = TextEditingController();
    final passCtrl = TextEditingController(text: 'Staff@123');
    String dept = 'Kỹ thuật';
    String role = 'Employee';

    await showDialog(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (context, setDialogState) => AlertDialog(
          title: const Text('Thêm nhân viên mới'),
          content: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                TextField(controller: usernameCtrl, decoration: const InputDecoration(labelText: 'Tên đăng nhập *')),
                TextField(controller: nameCtrl, decoration: const InputDecoration(labelText: 'Họ và tên *')),
                TextField(controller: emailCtrl, decoration: const InputDecoration(labelText: 'Email *')),
                TextField(controller: passCtrl, decoration: const InputDecoration(labelText: 'Mật khẩu *')),
                const SizedBox(height: 10),
                DropdownButtonFormField<String>(
                  initialValue: dept,
                  decoration: const InputDecoration(labelText: 'Phòng ban'),
                  items: const [
                    DropdownMenuItem(value: 'Kỹ thuật', child: Text('Kỹ thuật')),
                    DropdownMenuItem(value: 'Nhân sự', child: Text('Nhân sự')),
                    DropdownMenuItem(value: 'Kinh doanh', child: Text('Kinh doanh')),
                    DropdownMenuItem(value: 'Ban Giám Đốc', child: Text('Ban Giám Đốc')),
                  ],
                  onChanged: (val) {
                    if (val != null) setDialogState(() => dept = val);
                  },
                ),
                DropdownButtonFormField<String>(
                  initialValue: role,
                  decoration: const InputDecoration(labelText: 'Vai trò'),
                  items: const [
                    DropdownMenuItem(value: 'Employee', child: Text('Nhân viên')),
                    DropdownMenuItem(value: 'Manager', child: Text('Quản lý')),
                    DropdownMenuItem(value: 'Admin', child: Text('Admin')),
                  ],
                  onChanged: (val) {
                    if (val != null) setDialogState(() => role = val);
                  },
                ),
              ],
            ),
          ),
          actions: [
            TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('Hủy')),
            ElevatedButton(
              onPressed: () async {
                if (usernameCtrl.text.isEmpty || nameCtrl.text.isEmpty || emailCtrl.text.isEmpty) return;
                Navigator.pop(ctx);
                final ok = await _service.createEmployee({
                  'username': usernameCtrl.text.trim(),
                  'fullName': nameCtrl.text.trim(),
                  'email': emailCtrl.text.trim(),
                  'password': passCtrl.text.trim(),
                  'department': dept,
                  'role': role,
                });
                if (ok && context.mounted) {
                  _loadEmployees();
                  ScaffoldMessenger.of(context).showSnackBar(
                    const SnackBar(content: Text('Thêm nhân viên thành công!'), backgroundColor: Colors.green),
                  );
                }
              },
              child: const Text('Lưu'),
            ),
          ],
        ),
      ),
    );
  }

  // === CRUD CA LÀM VIỆC ===
  Future<void> _showAddShiftDialog() async {
    final nameCtrl = TextEditingController();
    final startCtrl = TextEditingController(text: '08:00');
    final endCtrl = TextEditingController(text: '17:00');
    final rateCtrl = TextEditingController(text: '50000');

    await showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Thêm ca làm việc mới'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            TextField(controller: nameCtrl, decoration: const InputDecoration(labelText: 'Tên ca làm việc *')),
            TextField(controller: startCtrl, decoration: const InputDecoration(labelText: 'Giờ bắt đầu (HH:mm)')),
            TextField(controller: endCtrl, decoration: const InputDecoration(labelText: 'Giờ kết thúc (HH:mm)')),
            TextField(controller: rateCtrl, decoration: const InputDecoration(labelText: 'Đơn giá lương (đ/giờ)')),
          ],
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('Hủy')),
          ElevatedButton(
            onPressed: () async {
              if (nameCtrl.text.trim().isEmpty) return;
              Navigator.pop(ctx);
              final ok = await _service.createShift({
                'name': nameCtrl.text.trim(),
                'startTime': startCtrl.text.trim(),
                'endTime': endCtrl.text.trim(),
                'hourlyRate': double.tryParse(rateCtrl.text.trim()) ?? 50000,
                'lateGraceMinutes': 15,
              });
              if (ok && mounted) {
                _loadShifts();
                ScaffoldMessenger.of(context).showSnackBar(
                  const SnackBar(content: Text('Thêm ca làm việc thành công!'), backgroundColor: Colors.green),
                );
              }
            },
            child: const Text('Lưu'),
          ),
        ],
      ),
    );
  }

  // === CRUD PHÊ DUYỆT ĐƠN NGHỈ PHÉP ===
  Future<void> _handleUpdateLeaveStatus(int id, String status) async {
    final ok = await _service.updateLeaveStatus(id, status);
    if (ok && mounted) {
      _loadLeaveRequests();
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Đã cập nhật đơn: $status'), backgroundColor: Colors.green),
      );
    }
  }

  Future<void> _handleLogout() async {
    await AuthService.logout();
    if (mounted) {
      Navigator.pushAndRemoveUntil(
        context,
        MaterialPageRoute(builder: (context) => const LoginScreen()),
        (route) => false,
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('QUẢN TRỊ TOÀN CÔNG TY', style: TextStyle(fontSize: 17, fontWeight: FontWeight.bold)),
            Text('Admin: Bùi Nguyễn Công Nghiệp (Giám đốc)', style: TextStyle(fontSize: 12, color: Colors.white70)),
          ],
        ),
        backgroundColor: Colors.indigo[900],
        foregroundColor: Colors.white,
        actions: [
          IconButton(icon: const Icon(Icons.refresh), tooltip: 'Làm mới', onPressed: _loadAllData),
          IconButton(icon: const Icon(Icons.logout), tooltip: 'Đăng xuất', onPressed: _handleLogout),
        ],
        bottom: TabBar(
          controller: _tabController,
          isScrollable: true,
          indicatorColor: Colors.amber,
          labelColor: Colors.white,
          unselectedLabelColor: Colors.white70,
          tabs: const [
            Tab(icon: Icon(Icons.table_chart), text: 'Bảng Chấm Công'),
            Tab(icon: Icon(Icons.people), text: 'Nhân Viên (CRUD)'),
            Tab(icon: Icon(Icons.schedule), text: 'Ca Làm (CRUD)'),
            Tab(icon: Icon(Icons.check_circle_outline), text: 'Duyệt Đơn Nghỉ'),
            Tab(icon: Icon(Icons.qr_code_2), text: 'Mã Dynamic QR'),
          ],
        ),
      ),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : TabBarView(
              controller: _tabController,
              children: [
                _buildAttendanceTab(),
                _buildEmployeeTab(),
                _buildShiftTab(),
                _buildLeaveApprovalTab(),
                _buildDynamicQrStationTab(),
              ],
            ),
    );
  }

  // TAB 1: Bảng chấm công
  Widget _buildAttendanceTab() {
    if (_allAttendances.isEmpty) {
      return const Center(child: Text('Chưa có bản ghi chấm công nào'));
    }

    return ListView.builder(
      padding: const EdgeInsets.all(12),
      itemCount: _allAttendances.length,
      itemBuilder: (ctx, i) {
        final a = _allAttendances[i];
        final isLate = a.status == 'Đi muộn';

        return Card(
          margin: const EdgeInsets.only(bottom: 10),
          child: ListTile(
            leading: CircleAvatar(
              backgroundColor: isLate ? Colors.red[100] : Colors.green[100],
              child: Icon(isLate ? Icons.timer_off : Icons.check, color: isLate ? Colors.red : Colors.green),
            ),
            title: Text('${a.fullName ?? a.userName} (${a.department})',
                style: const TextStyle(fontWeight: FontWeight.bold)),
            subtitle: Text(
              'Ngày: ${dateFormat.format(a.date)} | Vào: ${a.checkInTime != null ? timeFormat.format(a.checkInTime!) : "--"} - Ra: ${a.checkOutTime != null ? timeFormat.format(a.checkOutTime!) : "--"}\n'
              'Phương thức: ${a.checkInMethod} • Ca: ${a.shiftName} • Tổng: ${a.totalHours}h'
              '${a.noiseLevelDb != null ? " • 🔊 ${a.noiseLevelDb!.toStringAsFixed(1)} dB" : ""}',
              style: const TextStyle(fontSize: 12),
            ),
            isThreeLine: true,
            trailing: Text(
              a.status,
              style: TextStyle(color: isLate ? Colors.red : Colors.green, fontWeight: FontWeight.bold),
            ),
          ),
        );
      },
    );
  }

  // TAB 2: Quản lý nhân viên (CRUD)
  Widget _buildEmployeeTab() {
    return Scaffold(
      backgroundColor: Colors.transparent,
      body: _employees.isEmpty
          ? const Center(child: Text('Chưa có nhân viên nào'))
          : ListView.builder(
              padding: const EdgeInsets.all(12),
              itemCount: _employees.length,
              itemBuilder: (ctx, i) {
                final emp = _employees[i];
                return Card(
                  margin: const EdgeInsets.only(bottom: 8),
                  child: ListTile(
                    leading: CircleAvatar(child: Text(emp.fullName.isNotEmpty ? emp.fullName[0] : 'U')),
                    title: Text(emp.fullName, style: const TextStyle(fontWeight: FontWeight.bold)),
                    subtitle: Text('${emp.position} • Phòng ${emp.department}\nVai trò: ${emp.role} • Lương CB: ${currencyFormat.format(emp.baseSalary)} đ'),
                    isThreeLine: true,
                    trailing: IconButton(
                      icon: const Icon(Icons.delete, color: Colors.red),
                      onPressed: () async {
                        if (emp.username == 'admin') {
                          ScaffoldMessenger.of(context).showSnackBar(
                            const SnackBar(content: Text('Không thể xóa tài khoản Admin!')),
                          );
                          return;
                        }
                        final ok = await _service.deleteEmployee(emp.id);
                        if (ok && mounted) {
                          _loadEmployees();
                          ScaffoldMessenger.of(context).showSnackBar(
                            const SnackBar(content: Text('Đã xóa nhân viên')),
                          );
                        }
                      },
                    ),
                  ),
                );
              },
            ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: _showAddEmployeeDialog,
        icon: const Icon(Icons.person_add),
        label: const Text('Thêm Nhân Viên'),
        backgroundColor: Colors.indigo[900],
        foregroundColor: Colors.white,
      ),
    );
  }

  // TAB 3: Quản lý ca làm việc (CRUD)
  Widget _buildShiftTab() {
    return Scaffold(
      backgroundColor: Colors.transparent,
      body: _shifts.isEmpty
          ? const Center(child: Text('Chưa có ca làm việc nào'))
          : ListView.builder(
              padding: const EdgeInsets.all(12),
              itemCount: _shifts.length,
              itemBuilder: (ctx, i) {
                final s = _shifts[i];
                return Card(
                  margin: const EdgeInsets.only(bottom: 8),
                  child: ListTile(
                    leading: const Icon(Icons.access_time, color: Colors.indigo),
                    title: Text(s.name, style: const TextStyle(fontWeight: FontWeight.bold)),
                    subtitle: Text('Thời gian: ${s.startTime} - ${s.endTime} | Đơn giá: ${currencyFormat.format(s.hourlyRate)} đ/h'),
                    trailing: IconButton(
                      icon: const Icon(Icons.delete, color: Colors.red),
                      onPressed: () async {
                        final ok = await _service.deleteShift(s.id);
                        if (ok && mounted) {
                          _loadShifts();
                        }
                      },
                    ),
                  ),
                );
              },
            ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: _showAddShiftDialog,
        icon: const Icon(Icons.add_alarm),
        label: const Text('Thêm Ca Làm'),
        backgroundColor: Colors.indigo[900],
        foregroundColor: Colors.white,
      ),
    );
  }

  // TAB 4: Phê duyệt đơn nghỉ phép
  Widget _buildLeaveApprovalTab() {
    if (_allLeaveRequests.isEmpty) {
      return const Center(child: Text('Không có đơn xin nghỉ phép nào'));
    }

    return ListView.builder(
      padding: const EdgeInsets.all(12),
      itemCount: _allLeaveRequests.length,
      itemBuilder: (ctx, i) {
        final req = _allLeaveRequests[i];
        final isPending = req.status == 'Chờ duyệt';

        return Card(
          margin: const EdgeInsets.only(bottom: 10),
          child: Padding(
            padding: const EdgeInsets.all(12.0),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text(req.fullName ?? req.userName ?? 'Nhân viên',
                        style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 15)),
                    Text(dateFormat.format(req.startDate), style: const TextStyle(color: Colors.grey, fontSize: 12)),
                  ],
                ),
                const SizedBox(height: 4),
                Text('Phòng ban: ${req.department ?? "Kỹ thuật"} • Loại: ${req.leaveType}'),
                Text('Lý do: ${req.reason}', style: const TextStyle(fontStyle: FontStyle.italic)),
                const SizedBox(height: 8),
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text('Trạng thái: ${req.status}',
                        style: TextStyle(
                            fontWeight: FontWeight.bold,
                            color: req.status == 'Đã duyệt'
                                ? Colors.green
                                : req.status == 'Từ chối'
                                    ? Colors.red
                                    : Colors.orange)),
                    if (isPending)
                      Row(
                        children: [
                          ElevatedButton(
                            onPressed: () => _handleUpdateLeaveStatus(req.id, 'Đã duyệt'),
                            style: ElevatedButton.styleFrom(backgroundColor: Colors.green, foregroundColor: Colors.white),
                            child: const Text('Duyệt'),
                          ),
                          const SizedBox(width: 8),
                          OutlinedButton(
                            onPressed: () => _handleUpdateLeaveStatus(req.id, 'Từ chối'),
                            style: OutlinedButton.styleFrom(foregroundColor: Colors.red),
                            child: const Text('Từ chối'),
                          ),
                        ],
                      ),
                  ],
                ),
              ],
            ),
          ),
        );
      },
    );
  }

  // TAB 5: TRẠM PHÁT DYNAMIC QR (TÍNH NĂNG MỚI ĐỀ XUẤT 1.0đ)
  Widget _buildDynamicQrStationTab() {
    return Center(
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(24.0),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
              decoration: BoxDecoration(color: Colors.indigo[50], borderRadius: BorderRadius.circular(20)),
              child: const Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Icon(Icons.security, color: Colors.indigo, size: 20),
                  SizedBox(width: 8),
                  Text('CHỐNG ĐIỂM DANH HỘ: DYNAMIC QR 15 GIÂY',
                      style: TextStyle(fontWeight: FontWeight.bold, color: Colors.indigo, fontSize: 13)),
                ],
              ),
            ),
            const SizedBox(height: 20),

            // Mã QR Visualizer
            Container(
              width: 240,
              height: 240,
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(16),
                border: Border.all(color: Colors.indigo.shade200, width: 4),
                boxShadow: [
                  BoxShadow(color: Colors.black.withValues(alpha: 0.08), blurRadius: 15, spreadRadius: 3),
                ],
              ),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  const Icon(Icons.qr_code_2, size: 140, color: Colors.indigo),
                  const SizedBox(height: 6),
                  Text(
                    _dynamicQrToken?.substring(0, 20) ?? 'CHAMCONG_QR_TOKEN',
                    style: const TextStyle(fontSize: 10, color: Colors.grey, fontFamily: 'monospace'),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 20),

            // Đồng hồ đếm ngược
            Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                const Icon(Icons.timer, color: Colors.deepOrange),
                const SizedBox(width: 6),
                Text(
                  'Tự động làm mới mã sau: $_qrCountdown giây',
                  style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold, color: Colors.deepOrange),
                ),
              ],
            ),
            const SizedBox(height: 12),
            const Text(
              'Màn hình này được chiếu tại quầy lễ tân / sảnh công ty.\n'
              'Mã QR tự thay đổi mỗi 15 giây để ngăn nhân viên chụp ảnh gửi cho đồng nghiệp check-in hộ.',
              textAlign: TextAlign.center,
              style: TextStyle(fontSize: 12, color: Colors.black54),
            ),
          ],
        ),
      ),
    );
  }
}
