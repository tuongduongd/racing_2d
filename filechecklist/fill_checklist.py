"""
Script Python điền mức tự đánh giá sau nâng cấp (trung thực theo hiện trạng mã nguồn & kết quả kiểm chứng)
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
        "SV (MSSV – Họ tên): [SV tự điền]   Git: tungduongd (22 commits + bản nâng cấp)"
    ),
    "A5": (
        "TỰ KÊ KHAI KHỐI LƯỢNG (SV điền): Thể loại/PCU: Đua xe Pseudo-3D (4 Map JSON + Config JSON + Mid-Race Save + Police + Kick + Garage + 4 Skin + CRT + BGM) | "
        "UC: 9 UC | Entity/UML: 4 .asmdef, 71 runtime (25 Pure Core 0 UnityEngine + 6 Adapters + 40 Game) + 3 Editor / 30+ lớp | "
        "Thuật toán/AI: 5 thuật toán + O(.) | GoF: 6 mẫu đủ 3 nhóm (Factory, Decorator, Adapter, State, Strategy, Observer) | "
        "Tests: 109/109 EditMode PASS (20 files, 4.342s, 0 warnings) | Build: Builds/RoadRash2D/RoadRash2D.exe"
    ),
}

# (row, ma_tieu_chi, diem_chu, check_x, minh_chung)
SELF_ASSESSMENT = [
    (
        8, "1.1", "A", "X",
        "Mục 2.1, 8.2 BC | 01..04_*.json; TrackDefinition.cs; PoliceChaseController.cs; RaceCombatSystem.cs | "
        "4 Map JSON (2-6 làn) + Cảnh sát truy đuổi + Đá đối thủ + 3 mức AI + 9 UC",
    ),
    (
        9, "1.2", "A", "X",
        "Mục 8.2 BC | ExternalJsonTrackLoader.cs; ExternalBalanceConfigLoader.cs; MidRaceSaveService.cs; PauseMenuUI.cs; GameMenuUI.cs | "
        "4 Map JSON + game_balance.json + Save/Load giữa trận (Mid-Race) + HighScoreEntry top điểm",
    ),
    (
        10, "1.3", "A", "X",
        "Mục 2.4, 7, 8.2 BC; game_balance.json; EXPERIMENT.md | AIRacerDriver.cs; PoliceRacerDriver.cs; PoliceInterceptorDecorator | "
        "Bảng cân bằng chi tiết, 2 AI riêng, 5 vật cản, 4 Upgrade Tiers, Benchmark 100% winnable",
    ),
    (
        11, "1.4", "B", "X",
        "README.md; .gitignore; Builds/RoadRash2D/RoadRash2D.exe | RoadRashSceneSetup.BuildWindowsRelease() | "
        "README gốc đủ hướng dẫn, Build Windows sạch + StreamingAssets/, .gitignore chuẩn, 22+ commits (chờ SV quay Video 3-5p & xuất Slide PPTX; lịch sử git 10 ngày < 3 tuần)",
    ),
    (
        12, "2.1", "A", "X",
        "Mục 6, 8.2 BC | LobbyUiBuilder.cs; GarageUiBuilder.cs; GameMenuUI.cs; RaceHUD.cs; PauseMenuUI.cs; CrtScanlineController.cs; RaceVfxController.cs; RaceAudioController.cs | "
        "uGUI+TMP Cyber Arcade đủ luồng (Save/Continue/Replay reset sạch), BGM Synthwave 120 BPM + 11 SFX + PlayerPrefs Volume/Mute, URP Bloom + CRT",
    ),
    (
        13, "2.2", "A", "X",
        "Mục 3.4, 8.2, 9 BC; BUG_LOG.md | GameDomainException.cs (+4 lớp con); CarUpgradeSystem.cs (LoadStrict, BuyUpgradeStrict); CollisionSolverMath.cs | "
        "Cây GameDomainException đầy đủ, 0 catch {} rỗng, fallback/clamping an toàn JSON hỏng, vật lý va chạm Newton + wall-slide",
    ),
    (
        14, "2.3", "A", "X",
        "Mục 2.3 NFR-01 BC | GameManager.cs; RoadRashRenderer2D.cs (_opponentPool, _propPool); BikePaletteSwapper.cs; StreamingAssets/ | "
        "FixedUpdate 50Hz, Object Pool sprite, Palette Swap cache Dictionary (0 alloc Update), nạp JSON đường dẫn tương đối StreamingAssets/",
    ),
    (
        15, "2.4", "A", "X",
        "Mục 8.1 BC | Assets/Scripts/ (4 .asmdef, 71 runtime + 3 Editor .cs) | "
        "SRP RaceCombatSystem/Builders, 0 lỗi biên dịch, 0 CS warnings, 0 UAC1001 ([Serializable] VehicleInput), 0 ?. trên UnityEngine.Object, đã xóa Assets/Welcome/",
    ),
    (
        16, "3.1", "A", "X",
        "Mục 2, 5.1 BC | GameManager.cs; LapCheckpointManager.cs; CarUpgradeSystem.cs; ProgressionRuleEngine.cs | "
        "9 UC ánh xạ 1-1; FixedUpdate 50Hz rõ; 0 luật thắng/tiền ẩn trong UI",
    ),
    (
        17, "3.2", "A", "X",
        "Mục 4, 8.2 BC | Core/Pure/Domain/ (17 files) + TrackDefinition + Car + Driver + StreamingAssets/*.json | "
        ">=17 lớp Entity/VO/Enum thực chất + nạp Map JSON & game_balance.json ngoài",
    ),
    (
        18, "3.3", "A", "X",
        "Mục 7 BC | CollisionSolverMath.cs; CheckpointGateMath.cs; WaypointTrack.cs; ProgressionRuleEngine.cs; InputHandler.cs | "
        "5 thuật toán lõi kèm bảng độ phức tạp O(.); cô lập Keyboard.current 100% chỉ còn trong InputHandler.cs (0 đọc phím trong GameState/Audio/UI)",
    ),
    (
        19, "4.1", "A", "X",
        "Mục 4.1 BC | BAO_CAO_KIEN_TRUC_OOP.md (30+ lớp) <-> Assets/Scripts/ | "
        "30+ lớp thật, 0 lớp ma, đủ 6/6 quan hệ UML (Realization ..|>, Inheritance, sửa GameManager o-- GameState)",
    ),
    (
        20, "4.2", "A", "X",
        "Mục 5, 6 BC | GameState.cs; RaceStateTransitionRules.cs; PauseMenuUI.cs; MidRaceSaveService.cs | "
        "1 State Diagram đủ [Guard] / Action + 2 Sequence Diagrams khớp call stack DI & Mid-Race Save",
    ),
    (
        21, "4.3", "A", "X",
        "Mục 4.2, 8.1 BC | RacingGame.Core.asmdef (noEngineReferences: true); Core/Adapters/ | "
        "Package Diagram 3 tầng; 25 Pure Core files với 0 using UnityEngine (TC-91 PASS), 0 Circular Dependency",
    ),
    (
        22, "4.4", "A", "X",
        "Mục 3 BC | VehicleStatsDecorators.cs; ProgressionRuleEngine.cs; Driver.cs (UsesArcadeAssist); GameState.cs (RemainingCountdown) | "
        "Bất biến miền chặt, LSP chuẩn đa hình, 0 downcast is PlayerDriver / is CountdownState, 0 override rỗng",
    ),
    (
        23, "4.5", "A", "X",
        "Mục 3, 4 BC | Core/Pure/Contracts/ (8+ Interfaces); GameManager.ConfigureServices; RaceCombatSystem.cs | "
        "SRP tách Combat/Progression/MidRaceSave, 8 ISP interfaces, Constructor/Method DI Composition Root",
    ),
    (
        24, "4.6", "A", "X",
        "Mục 4.3 BC | TrackObstacleFactory (Factory); VehicleStatsDecorators (Decorator); File/Json/Audio/Track Adapters; GameState; Driver; Observer events | "
        "6 mẫu GoF phủ đủ cả 3 nhóm (Creational, Structural, Behavioral) + Test kiểm chứng mở rộng OCP & Live Swap",
    ),
    (
        25, "5.1", "A", "X",
        "Mục 9 BC; TEST_CASES.md | PureCoreArchitectureAndDipTests.cs; ExternalJsonMidRaceSaveAndBgmTests.cs (20 test files, 109 tests) | "
        "109/109 PASS (4.342s), Ma trận EP/BVA/State/Exception/Integration, 4 Stub/Fake qua Interface (InMemorySaveStorage, FakeInputProvider, FakeTrackDataProvider, StubDriver)",
    ),
    (
        26, "5.2", "A", "X",
        "Mục 9 BC; BUG_LOG.md | TC-82, TC-109 (Observer); Mid-Race Save Round-Trip; External JSON Tests | "
        "Integration Round-Trip + test hủy PoliceEngaged/SkinChanged ở OnDisable/OnDestroy PASS, 6 Bug Log 6 cột + 10 kịch bản Playtest PT-01..PT-10",
    ),
    (
        27, "5.3", "A", "X",
        "Mục 9 BC; TEST_CASES.md; BUG_LOG.md; Logs/EditModeResults.xml | 20 files, 109 tests | "
        "109/109 Automated Tests PASS 100% (0 Failed, 0 Skipped, 0 Warning) + 3 kịch bản Fault-Injection kiểm chứng thực chất",
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


def clear_cell_value(sheet_xml: str, ref: str) -> str:
    pattern = re.compile(rf'<c r="{ref}"([^>]*?)(?:/>|>.*?</c>)')
    m = pattern.search(sheet_xml)
    if not m:
        return sheet_xml
    attrs = re.sub(r'\s+t="[^"]*"', "", m.group(1))
    replacement = f'<c r="{ref}"{attrs}/>'
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
        else:
            sheet1_xml = clear_cell_value(sheet1_xml, f"G{row}")

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

    def classify_letter_grade(score: float) -> str:
        if score < 4.0:
            return "Xếp loại: F (Trượt)"
        if score < 5.0:
            return "Xếp loại: D"
        if score < 5.5:
            return "Xếp loại: D+"
        if score < 6.5:
            return "Xếp loại: C"
        if score < 7.0:
            return "Xếp loại: C+"
        if score < 8.0:
            return "Xếp loại: B"
        if score < 8.5:
            return "Xếp loại: B+"
        if score < 9.5:
            return "Xếp loại: A"
        return "Xếp loại: A+"

    def classify_band(score: float) -> str:
        if score < 5.5:
            return "Yếu / Kém"
        if score < 7.0:
            return "Trung bình"
        if score < 8.5:
            return "Khá"
        return "Xuất sắc / Giỏi"

    sheet1_xml = set_formula_cached_value(sheet1_xml, "E28", "Đủ 20/20", is_string=True)
    sheet1_xml = set_formula_cached_value(sheet1_xml, "F28", total, is_string=False)
    sheet1_xml = set_formula_cached_value(sheet1_xml, "G28", check_count, is_string=False)
    sheet1_xml = set_formula_cached_value(sheet1_xml, "E30", epistemic_label, is_string=True)
    sheet1_xml = set_formula_cached_value(sheet1_xml, "F30", epistemic_cap, is_string=False)
    sheet1_xml = set_formula_cached_value(sheet1_xml, "E31", "Không bị chặn" if final_score >= total else "Bị giới hạn trần", is_string=True)
    sheet1_xml = set_formula_cached_value(sheet1_xml, "F31", final_score, is_string=False)
    sheet1_xml = set_formula_cached_value(sheet1_xml, "G31", classify_band(final_rounded), is_string=True)
    sheet1_xml = set_formula_cached_value(sheet1_xml, "F32", final_rounded, is_string=False)
    sheet1_xml = set_formula_cached_value(sheet1_xml, "G32", classify_letter_grade(final_rounded), is_string=True)

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
