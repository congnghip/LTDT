import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../models/app_models.dart';
import '../services/auth_service.dart';
import '../services/chamcong_service.dart';
import 'login_screen.dart';

class ManagerDashboardScreen extends StatefulWidget {
  const ManagerDashboardScreen({super.key});

  @override
  State<ManagerDashboardScreen> createState() => _ManagerDashboardScreenState();
}

class _ManagerDashboardScreenState extends State<ManagerDashboardScreen> with SingleTickerProviderStateMixin {
  final ChamCongService _service = ChamCongService();
  late TabController _tabController;

  List<AttendanceModel> _attendances = [];
  List<LeaveRequestModel> _pendingLeaves = [];
  bool _isLoading = false;

  final dateFormat = DateFormat("dd/MM/yyyy");
  final timeFormat = DateFormat("HH:mm");

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 2, vsync: this);
    _loadData();
  }

  Future<void> _loadData() async {
    setState(() => _isLoading = true);
    await Future.wait([
      _loadAttendances(),
      _loadPendingLeaves(),
    ]);
    setState(() => _isLoading = false);
  }

  Future<void> _loadAttendances() async {
    final list = await _service.getAllAttendances(date: DateTime.now());
    setState(() => _attendances = list);
  }

  Future<void> _loadPendingLeaves() async {
    final list = await _service.getAllLeaveRequests(status: 'Chờ duyệt');
    setState(() => _pendingLeaves = list);
  }

  Future<void> _handleApproveLeave(int id, String status) async {
    final ok = await _service.updateLeaveStatus(id, status);
    if (ok && mounted) {
      _loadPendingLeaves();
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
    final checkedInCount = _attendances.length;
    final lateCount = _attendances.where((a) => a.status == 'Đi muộn').length;

    return Scaffold(
      appBar: AppBar(
        title: const Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('DASHBOARD QUẢN LÝ CA TRỰC', style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold)),
            Text('Quản lý: Trần Thị Bình (Trưởng phòng)', style: TextStyle(fontSize: 12, color: Colors.white70)),
          ],
        ),
        backgroundColor: Colors.purple[800],
        foregroundColor: Colors.white,
        actions: [
          IconButton(icon: const Icon(Icons.refresh), onPressed: _loadData),
          IconButton(icon: const Icon(Icons.logout), onPressed: _handleLogout),
        ],
        bottom: TabBar(
          controller: _tabController,
          indicatorColor: Colors.amber,
          labelColor: Colors.white,
          unselectedLabelColor: Colors.white70,
          tabs: [
            Tab(icon: const Icon(Icons.bar_chart), text: 'Giám sát ca (${_attendances.length})'),
            Tab(icon: const Icon(Icons.approval), text: 'Duyệt đơn (${_pendingLeaves.length})'),
          ],
        ),
      ),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : TabBarView(
              controller: _tabController,
              children: [
                // TAB 1: Giám sát ca trực
                _buildLiveMonitorTab(checkedInCount, lateCount),

                // TAB 2: Duyệt đơn xin nghỉ
                _buildLeaveApprovalTab(),
              ],
            ),
    );
  }

  Widget _buildLiveMonitorTab(int checkedIn, int late) {
    return Column(
      children: [
        // Thẻ tóm tắt tình hình ca trực
        Container(
          padding: const EdgeInsets.all(16),
          color: Colors.purple[50],
          child: Row(
            children: [
              Expanded(
                child: _buildSummaryBox('Đã Check-in hôm nay', '$checkedIn người', Colors.purple),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: _buildSummaryBox('Đi muộn', '$late người', Colors.red),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: _buildSummaryBox('Đúng giờ', '${checkedIn - late} người', Colors.green),
              ),
            ],
          ),
        ),

        // Danh sách nhân viên trong ca
        Expanded(
          child: _attendances.isEmpty
              ? const Center(child: Text('Chưa có nhân viên nào check-in hôm nay'))
              : ListView.builder(
                  padding: const EdgeInsets.all(12),
                  itemCount: _attendances.length,
                  itemBuilder: (ctx, i) {
                    final a = _attendances[i];
                    final isLate = a.status == 'Đi muộn';

                    return Card(
                      margin: const EdgeInsets.only(bottom: 8),
                      child: ListTile(
                        leading: CircleAvatar(
                          backgroundColor: isLate ? Colors.red[100] : Colors.green[100],
                          child: Icon(isLate ? Icons.warning : Icons.check, color: isLate ? Colors.red : Colors.green),
                        ),
                        title: Text(a.fullName ?? a.userName ?? 'Nhân viên',
                            style: const TextStyle(fontWeight: FontWeight.bold)),
                        subtitle: Text('Vào: ${a.checkInTime != null ? timeFormat.format(a.checkInTime!) : "--"} | Phương thức: ${a.checkInMethod}\nPhòng: ${a.department}${a.noiseLevelDb != null ? " • 🔊 ${a.noiseLevelDb!.toStringAsFixed(1)} dB" : ""}'),
                        isThreeLine: true,
                        trailing: Text(a.status,
                            style: TextStyle(color: isLate ? Colors.red : Colors.green, fontWeight: FontWeight.bold)),
                      ),
                    );
                  },
                ),
        ),
      ],
    );
  }

  Widget _buildSummaryBox(String title, String value, MaterialColor color) {
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: color.shade200),
      ),
      child: Column(
        children: [
          Text(value, style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold, color: color[800])),
          const SizedBox(height: 4),
          Text(title, style: TextStyle(fontSize: 10, color: Colors.grey[700]), textAlign: TextAlign.center),
        ],
      ),
    );
  }

  Widget _buildLeaveApprovalTab() {
    if (_pendingLeaves.isEmpty) {
      return const Center(child: Text('Không có đơn xin nghỉ phép nào cần duyệt'));
    }

    return ListView.builder(
      padding: const EdgeInsets.all(12),
      itemCount: _pendingLeaves.length,
      itemBuilder: (ctx, i) {
        final req = _pendingLeaves[i];
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
                    Text(req.fullName ?? 'Nhân viên', style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 15)),
                    Text(dateFormat.format(req.startDate), style: const TextStyle(color: Colors.grey, fontSize: 12)),
                  ],
                ),
                Text('Loại nghỉ: ${req.leaveType} | Phòng: ${req.department ?? "Kỹ thuật"}'),
                Text('Lý do: ${req.reason}', style: const TextStyle(fontStyle: FontStyle.italic)),
                const SizedBox(height: 10),
                Row(
                  mainAxisAlignment: MainAxisAlignment.end,
                  children: [
                    ElevatedButton(
                      onPressed: () => _handleApproveLeave(req.id, 'Đã duyệt'),
                      style: ElevatedButton.styleFrom(backgroundColor: Colors.green, foregroundColor: Colors.white),
                      child: const Text('Duyệt đơn'),
                    ),
                    const SizedBox(width: 8),
                    OutlinedButton(
                      onPressed: () => _handleApproveLeave(req.id, 'Từ chối'),
                      style: OutlinedButton.styleFrom(foregroundColor: Colors.red),
                      child: const Text('Từ chối'),
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
}
