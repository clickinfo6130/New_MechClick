using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using PartManager.UI;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(PartManager.Commands.UICommands))]

namespace PartManager.Commands
{
    public class UICommands
    {
        /// <summary>
        /// IPC 연결 상태 확인
        /// </summary>
        [CommandMethod("IPCSTATUS")]
        public void CheckIPCStatus()
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;

            ed.WriteMessage("\n");
            ed.WriteMessage("\n╔════════════════════════════════════════╗");
            ed.WriteMessage("\n║       IPC 연결 상태 진단              ║");
            ed.WriteMessage("\n╚════════════════════════════════════════╝");

            bool isConnected = PaletteManager.GetIPCStatus();
            ed.WriteMessage($"\nC# 클라이언트 상태: {(isConnected ? "✅ 연결됨" : "❌ 연결 안 됨")}");
            ed.WriteMessage("\nNamed Pipe 이름: PartManager_IPC_Pipe");

            ed.WriteMessage("\n");
            ed.WriteMessage("\n해결 방법:");
            ed.WriteMessage("\n1. C++ ARX가 로드되었는지 확인: ARX 명령");
            ed.WriteMessage("\n2. C++ ARX 다시 로드: ARX → Unload → Load");
            ed.WriteMessage("\n3. IPC 재연결: IPCRECONNECT 명령");
            ed.WriteMessage("\n════════════════════════════════════════\n");
        }

        /// <summary>
        /// IPC 재연결
        /// </summary>
        [CommandMethod("IPCRECONNECT")]
        public void ReconnectIPC()
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;

            ed.WriteMessage("\n🔄 IPC 재연결 시도 중...");

            bool success = PaletteManager.ReconnectIPC();

            if (success)
            {
                ed.WriteMessage("\n✅ IPC 재연결 성공!");
            }
            else
            {
                ed.WriteMessage("\n❌ IPC 재연결 실패");
                ed.WriteMessage("\nC++ ARX가 로드되었는지 확인하세요: ARX 명령");
            }
        }

        /// <summary>
        /// 모니터 디버그 정보
        /// </summary>
        [CommandMethod("UIDEBUG")]
        public void ShowDebugInfo()
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;

            ed.WriteMessage("\n");
            ed.WriteMessage("\n╔════════════════════════════════════╗");
            ed.WriteMessage("\n║     모니터 정보                   ║");
            ed.WriteMessage("\n╚════════════════════════════════════╝");

            var screens = System.Windows.Forms.Screen.AllScreens;
            var primary = System.Windows.Forms.Screen.PrimaryScreen;

            ed.WriteMessage($"\n총 모니터 수: {screens.Length}");
            ed.WriteMessage($"\n주 모니터: {primary.DeviceName}");

            for (int i = 0; i < screens.Length; i++)
            {
                var screen = screens[i];
                ed.WriteMessage($"\n\n모니터 {i + 1}: {screen.DeviceName}");
                ed.WriteMessage($"\n  전체 영역: {screen.Bounds}");
                ed.WriteMessage($"\n  작업 영역: {screen.WorkingArea}");
                ed.WriteMessage($"\n  주 모니터: {(screen.Primary ? "예" : "아니오")}");
            }

            ed.WriteMessage("\n════════════════════════════════════\n");
        }

        /// <summary>
        /// UI 표시
        /// </summary>
        [CommandMethod("SHOWUI")]
        public void ShowUI()
        {
            PaletteManager.Show();
        }

        /// <summary>
        /// UI 숨기기
        /// </summary>
        [CommandMethod("HIDEUI")]
        public void HideUI()
        {
            PaletteManager.Hide();
        }

        /// <summary>
        /// UI 토글
        /// </summary>
        [CommandMethod("TOGGLEUI")]
        public void ToggleUI()
        {
            PaletteManager.Toggle();
        }

        /// <summary>
        /// 도킹 위치 설정
        /// </summary>
        [CommandMethod("DOCKUI")]
        public void DockUI()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            var options = new PromptKeywordOptions(
                "\n도킹 위치를 선택하세요 [왼쪽(L)/오른쪽(R)/플로팅(F)]:");
            options.Keywords.Add("L");
            options.Keywords.Add("R");
            options.Keywords.Add("F");
            options.Keywords.Default = "L";

            var result = ed.GetKeywords(options);
            if (result.Status != PromptStatus.OK)
                return;

            switch (result.StringResult)
            {
                case "L":
                    PaletteManager.SetDockPosition(Autodesk.AutoCAD.Windows.DockSides.Left);
                    ed.WriteMessage("\nUI를 왼쪽에 도킹했습니다.");
                    break;
                case "R":
                    PaletteManager.SetDockPosition(Autodesk.AutoCAD.Windows.DockSides.Right);
                    ed.WriteMessage("\nUI를 오른쪽에 도킹했습니다.");
                    break;
                case "F":
                    PaletteManager.SetDockPosition(Autodesk.AutoCAD.Windows.DockSides.None);
                    ed.WriteMessage("\nUI를 플로팅 모드로 설정했습니다.");
                    break;
            }
        }

        /// <summary>
        /// 부품 선택 창 열기
        /// </summary>
        [CommandMethod("PARTSELECT")]
        public void OpenPartSelector()
        {
            PaletteManager.Show();
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            ed.WriteMessage("\n부품 선택 패널이 열렸습니다.");
        }
    }
}
