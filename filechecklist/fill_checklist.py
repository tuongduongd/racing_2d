"""
Script Python điền sẵn mức tự đánh giá lần 1 (trung thực theo hiện trạng mã nguồn)
và cột Minh chứng vào sheet "Nhóm 01 người" của Checklist_BTL_Game_v2026.xlsx.

Không phụ thuộc thư viện ngoài (chỉ dùng zipfile, re, xml.sax.saxutils trong stdlib),
giữ nguyên 100% định dạng, style, công thức, Data Validation và các sheet khác.
"""

from pathlib import Path
import re
import zipfile
from xml.sax.saxutils import escape

XLSX_PATH = Path(__file__).resolve().parent / "Checklist_BTL_Game_v2026.xlsx"

GRADE_VALUES = {
    "A": 0.5,
    "B": 0.375,
    "C": 0.25,
    "D": 0.125,
    "F": 0.0,
}

HEADER_UPDATES = {
    "A4": (
        "Mã nhóm: Nhóm 01 SV   Tên đề tài: Road Rash 2D: Đại Chiến Shipper (newbie)   "
        "Phân nhánh: Nhánh 1 - Đua xe Real-time (Vật lý 2D & AI)   "
        "SV (MSSV – Họ tên): [SV tự điền]   Git: tungduongd (6 commits)"
    ),
    "A5": (
        "TỰ KÊ KHAI KHỐI LƯỢNG (SV điền): Thể loại/PCU: Đua xe Pseudo-3D (4 Map SO + Police Chase + Kick + Garage + 4 Skin) | "
        "UC: 9 UC | Entity/UML: 34+/26 lớp | Thuật toán/AI: 4 | GoF: 3 mẫu (1 nhóm Behavioral) | "
        "Tests: 70 EditMode PASS (Playtest: 0 ca, Bug Log: 2 sửa + 3 cảnh báo) | Git: 6 commits (3 ngày, +42 thay đổi)"
    ),
}

# (row, ma_tieu_chi, diem_chu, check_x, minh_chung)
SELF_ASSESSMENT = [
    (
        8, "1.1", "B", "X",
        "Mục 2.1, 8.2 BC | TrackDefinition.cs; Map_01..04.asset; PoliceChaseController.cs | "
        "4 Map SO (2-6 làn) + Cảnh sát + 3 mức AI + 9 UC (thiếu map JSON rời)",
    ),
    (
        9, "1.2", "C", "",
        "Mục 8.2 BC | CarUpgradeSystem.cs; MapManager.cs; SkinManager.cs | "
        "Lưu JSON racing2d_save.json + PlayerPrefs (thiếu Save giữa trận & JSON map)",
    ),
    (
        10, "1.3", "B", "X",
        "Mục 7, 8.2 BC; EXPERIMENT.md | AIRacerDriver.cs; PoliceRacerDriver.cs; TrackObstacleSystem.cs | "
        "2 AI riêng, 5 vật cản, 4 nâng cấp, Benchmark 3 vòng 2 map đạt 100%",
    ),
    (
        11, "1.4", "D", "",
        "Docs/README.md; .gitignore | Git log | "
        ".gitignore chuẩn Unity/VS; 6 commits/3 ngày (thiếu README gốc, Build sạch, Video, >=12 commits)",
    ),
    (
        12, "2.1", "B", "X",
        "Mục 6, 8.2 BC | GameMenuUI.cs; RaceHUD.cs; PauseMenuUI.cs; RaceVfxController.cs; RaceAudioController.cs | "
        "Đủ luồng UI, Reset sạch, VFX + 11 SFX phím M (thiếu BGM nhạc nền)",
    ),
    (
        13, "2.2", "C", "",
        "Mục 8.2, 9 BC | Car.cs; AIRacerDriver.cs; CarUpgradeSystem.cs:359,386; TC-09,29,61 | "
        "0 catch {} rỗng, kẹp JSON lỗi, STUCK=0 (chưa có cây CustomException)",
    ),
    (
        14, "2.3", "B", "X",
        "Mục 2.3 NFR-01 BC | GameManager.cs; RoadRashRenderer2D.cs (_opponentPool, _propPool); CarUpgradeSystem.cs | "
        "0 hard-code path, FixedUpdate 50Hz, Object Pool sprite",
    ),
    (
        15, "2.4", "B", "X",
        "Mục 8.1 BC | Assets/Scripts/ (14 thư mục, 3 .asmdef) | "
        "36 runtime .cs chuẩn naming, 0 lỗi biên dịch (còn Welcome2DScript.cs thừa)",
    ),
    (
        16, "3.1", "A", "X",
        "Mục 2, 5.1 BC | GameManager.cs; LapCheckpointManager.cs; CarUpgradeSystem.cs | "
        "9 UC ánh xạ 1-1; FixedUpdate rõ; 0 luật thắng/tiền ẩn trong UI",
    ),
    (
        17, "3.2", "B", "X",
        "Mục 4, 8.2 BC | Car, VehiclePhysics, VehicleStats, Driver, Checkpoint, RacerProgress, TrackDefinition... | "
        ">=12 lớp Entity/VO thực chất (chưa nạp JSON ngoài)",
    ),
    (
        18, "3.3", "B", "X",
        "Mục 7 BC | AIRacerDriver.cs; PoliceRacerDriver.cs; Checkpoint.cs; WaypointTrack.cs; InputHandler.cs | "
        "4 thuật toán lõi + tách InputHandler (phím Esc/P/M đọc ở Manager)",
    ),
    (
        19, "4.1", "C", "",
        "Mục 4 BC | BAO_CAO_KIEN_TRUC_OOP.md (26 lớp) <-> Assets/Scripts/ | "
        "26 lớp, 0 lớp ma (lệch GameManager *-- RaceStateBase & thiếu mũi tên Realization)",
    ),
    (
        20, "4.2", "B", "X",
        "Mục 5, 6 BC | GameManager.cs; GameState.cs | "
        "1 State Diagram + 2 Sequence Diagrams khớp chuỗi gọi hàm (cần thêm [Guard])",
    ),
    (
        21, "4.3", "D", "",
        "Mục 8.1 BC | RacingGame.asmdef (36 runtime .cs) | "
        "0 Circular Dependency nhưng 35/36 file có using UnityEngine & thiếu Package Diagram",
    ),
    (
        22, "4.4", "A", "X",
        "Mục 3 BC | VehicleStats.cs; CarUpgradeSystem.cs; LapCheckpointManager.cs; Driver.cs; GameState.cs | "
        ">=5 bất biến miền, LSP chuẩn, 0 downcast as, 0 override rỗng",
    ),
    (
        23, "4.5", "D", "",
        "GameManager.cs (965 dòng); Assets/Scripts/ | "
        "0 custom interface; GameManager ôm nhiều vai trò & new CarUpgradeSystem() trực tiếp",
    ),
    (
        24, "4.6", "C", "",
        "Mục 3, 4, 6 BC | RaceStateBase (State); Driver & IComparer (Strategy); C# event (Observer) | "
        "3 mẫu GoF chạy thật qua Call Stack, nhưng cùng 1 nhóm Behavioral",
    ),
    (
        25, "5.1", "B", "X",
        "Mục 9 BC; TEST_CASES.md | Assets/Tests/EditMode/ (14 files) | "
        "70/70 EditMode Tests PASS (7.726s), có StubDriver, phủ biên BVA & JSON lỗi",
    ),
    (
        26, "5.2", "C", "",
        "Mục 9 BC; BUG_LOG.md | TC-25,29,32,42,61,69,70 | "
        "Có >=4 Integration Tests PASS; thiếu Playtest >=8 ca, Bug Log 6 cột >=4 lỗi & test hủy Observer",
    ),
    (
        27, "5.3", "A", "X",
        "Mục 9 BC; TEST_CASES.md | Assets/Tests/EditMode/ (14 files, 70 tests) | "
        "70/70 Automated Tests PASS 100%, Assertion thực chất diệt đột biến tốt",
    ),
]


def append_shared_string(sst_xml: str, text: str) -> tuple[str, int]:
    m_uniq = re.search(r'uniqueCount="(\d+)"', sst_xml)
    m_cnt = re.search(r'count="(\d+)"', sst_xml)
    if not m_uniq or not m_cnt:
        raise ValueError("sharedStrings.xml thiếu count/uniqueCount")

    idx = int(m_uniq.group(1))
    new_cnt = int(m_cnt.group(1)) + 1
    new_uniq = idx + 1

    escaped = escape(text)
    preserve = ' xml:space="preserve"' if (text.startswith(" ") or text.endswith(" ")) else ""
    new_si = f"<si><t{preserve}>{escaped}</t></si>"

    sst_xml = re.sub(r'count="\d+"', f'count="{new_cnt}"', sst_xml, count=1)
    sst_xml = re.sub(r'uniqueCount="\d+"', f'uniqueCount="{new_uniq}"', sst_xml, count=1)
    sst_xml = sst_xml.replace("</sst>", new_si + "</sst>", 1)
    return sst_xml, idx


def set_cell_shared_string(sheet_xml: str, ref: str, ss_idx: int) -> str:
    pattern = re.compile(rf'<c r="{ref}"([^>]*?)(?:/>|>.*?</c>)')
    m = pattern.search(sheet_xml)
    if not m:
        raise ValueError(f"Không tìm thấy ô {ref} trong sheet1.xml")
    attrs = re.sub(r'\s+t="[^"]*"', "", m.group(1))
    replacement = f'<c r="{ref}"{attrs} t="s"><v>{ss_idx}</v></c>'
    return sheet_xml[:m.start()] + replacement + sheet_xml[m.end():]


def set_formula_cached_value(sheet_xml: str, ref: str, value, is_string: bool = False) -> str:
    cell_pattern = re.compile(rf'<c r="{ref}"([^>]*?)>((?:(?!</c>).)*?)</c>')
    m = cell_pattern.search(sheet_xml)
    if not m:
        raise ValueError(f"Không tìm thấy ô công thức {ref} trong sheet1.xml")

    attrs = re.sub(r'\s+t="[^"]*"', "", m.group(1))
    inner = m.group(2)
    f_match = re.search(r'(<f\b[^>]*?/>|<f\b[^>]*?>.*?</f>)', inner)
    if not f_match:
        raise ValueError(f"Ô {ref} không chứa thẻ <f>")

    formula_tag = f_match.group(1)
    if is_string:
        t_attr = ' t="str"'
        v_tag = f"<v>{escape(str(value))}</v>"
    else:
        t_attr = ""
        v_tag = f"<v>{value}</v>"

    replacement = f'<c r="{ref}"{attrs}{t_attr}>{formula_tag}{v_tag}</c>'
    return sheet_xml[:m.start()] + replacement + sheet_xml[m.end():]


def enable_full_calc_on_load(wb_xml: str) -> str:
    if 'fullCalcOnLoad="1"' in wb_xml:
        return wb_xml
    return re.sub(r"<calcPr\b", '<calcPr fullCalcOnLoad="1"', wb_xml, count=1)


def main() -> None:
    if not XLSX_PATH.exists():
        raise FileNotFoundError(f"Không tìm thấy file: {XLSX_PATH}")

    with zipfile.ZipFile(XLSX_PATH, "r") as zin:
        entries = {name: zin.read(name) for name in zin.namelist()}

    sst_xml = entries["xl/sharedStrings.xml"].decode("utf-8")
    sheet1_xml = entries["xl/worksheets/sheet1.xml"].decode("utf-8")
    wb_xml = entries["xl/workbook.xml"].decode("utf-8")

    # 1. Cập nhật dòng định danh & tự kê khai khối lượng (A4, A5)
    for ref, text in HEADER_UPDATES.items():
        sst_xml, idx = append_shared_string(sst_xml, text)
        sheet1_xml = set_cell_shared_string(sheet1_xml, ref, idx)

    # 2. Điền 20 tiêu chí (E8:E27, F8:F27 cached, G8:G27, H8:H27)
    row_scores = {}
    check_count = 0
    for row, _code, grade, check_mark, evidence in SELF_ASSESSMENT:
        sst_xml, g_idx = append_shared_string(sst_xml, grade)
        sheet1_xml = set_cell_shared_string(sheet1_xml, f"E{row}", g_idx)

        score = GRADE_VALUES[grade]
        row_scores[row] = score
        sheet1_xml = set_formula_cached_value(sheet1_xml, f"F{row}", score, is_string=False)

        if check_mark:
            sst_xml, x_idx = append_shared_string(sst_xml, check_mark)
            sheet1_xml = set_cell_shared_string(sheet1_xml, f"G{row}", x_idx)
            check_count += 1

        sst_xml, h_idx = append_shared_string(sst_xml, evidence)
        sheet1_xml = set_cell_shared_string(sheet1_xml, f"H{row}", h_idx)

    # 3. Cập nhật giá trị cache cho các ô tổng hợp để xem chính xác trước khi Excel mở lại
    nhom1 = sum(row_scores[r] for r in range(8, 12))
    nhom2 = sum(row_scores[r] for r in range(12, 16))
    truc1 = nhom1 + nhom2
    nhom3 = sum(row_scores[r] for r in range(16, 19))
    nhom4 = sum(row_scores[r] for r in range(19, 25))
    nhom5 = sum(row_scores[r] for r in range(25, 28))
    truc2 = nhom3 + nhom4 + nhom5
    total = truc1 + truc2
    rounded = round(total * 2.0) / 2.0
    epistemic_label = "FAIL_DEEP" if truc2 < 1.8 else ("WARN_DEEP" if truc2 < 3.0 else "PASS")
    epistemic_cap = 3.5 if epistemic_label == "FAIL_DEEP" else (6.5 if epistemic_label == "WARN_DEEP" else 10)
    final_score = min(total, 10, epistemic_cap)
    final_rounded = round(final_score * 2.0) / 2.0

    sheet1_xml = set_formula_cached_value(sheet1_xml, "E28", "Đủ 20/20", is_string=True)
    sheet1_xml = set_formula_cached_value(sheet1_xml, "F28", total, is_string=False)
    sheet1_xml = set_formula_cached_value(sheet1_xml, "G28", check_count, is_string=False)
    sheet1_xml = set_formula_cached_value(sheet1_xml, "E30", epistemic_label, is_string=True)
    sheet1_xml = set_formula_cached_value(sheet1_xml, "F30", epistemic_cap, is_string=False)
    sheet1_xml = set_formula_cached_value(sheet1_xml, "E31", "Không bị chặn" if final_score >= total else "Bị giới hạn trần", is_string=True)
    sheet1_xml = set_formula_cached_value(sheet1_xml, "F31", final_score, is_string=False)
    sheet1_xml = set_formula_cached_value(sheet1_xml, "G31", "Trung bình" if 5.5 <= final_rounded < 7 else ("Khá" if final_rounded < 8.5 else "Xuất sắc / Giỏi"), is_string=True)
    sheet1_xml = set_formula_cached_value(sheet1_xml, "F32", final_rounded, is_string=False)
    sheet1_xml = set_formula_cached_value(sheet1_xml, "G32", "Xếp loại: C+" if 6.5 <= final_rounded < 7 else "Xếp loại: B", is_string=True)

    summary_rows = [
        (30, truc1, 4.0),
        (31, nhom1, 2.0),
        (32, nhom2, 2.0),
        (33, truc2, 6.0),
        (34, nhom3, 1.5),
        (35, nhom4, 3.0),
        (36, nhom5, 1.5),
    ]
    for r, val, mx in summary_rows:
        sheet1_xml = set_formula_cached_value(sheet1_xml, f"L{r}", val, is_string=False)
        sheet1_xml = set_formula_cached_value(sheet1_xml, f"N{r}", val / mx, is_string=False)

    wb_xml = enable_full_calc_on_load(wb_xml)

    entries["xl/sharedStrings.xml"] = sst_xml.encode("utf-8")
    entries["xl/worksheets/sheet1.xml"] = sheet1_xml.encode("utf-8")
    entries["xl/workbook.xml"] = wb_xml.encode("utf-8")

    with zipfile.ZipFile(XLSX_PATH, "w", compression=zipfile.ZIP_DEFLATED) as zout:
        for name, data in entries.items():
            zout.writestr(name, data)

    print(
        f"Đã cập nhật {XLSX_PATH.name}: "
        f"Check={check_count}/20, Trục I={truc1:.3f}/4.0, Trục II={truc2:.3f}/6.0, "
        f"Tổng={total:.3f}/10 (Làm tròn {rounded:.1f}), Cổng 2={epistemic_label}."
    )


if __name__ == "__main__":
    main()
