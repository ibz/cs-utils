// Original C++ code at http://www.codeproject.com/KB/shell/ctrayiconposition.aspx
// Translated to C# by Ionut Bizau <ionut@bizau.ro>

using System;
using System.Text;
using System.Drawing;
using System.Runtime.InteropServices;

namespace TrayIconFinder
{
	public class TrayIconFinder
	{
		#region Win32API

		[Serializable, StructLayout(LayoutKind.Sequential)]
		struct RECT
		{
			public int Left;
			public int Top;
			public int Right;
			public int Bottom;

			public RECT(int Left, int Top, int Right, int Bottom) 
			{
				this.Left = Left;
				this.Top = Top;
				this.Right = Right;
				this.Bottom = Bottom;
			}

			public int Height { get { return Bottom - Top; } }
			public int Width { get { return Right - Left; } }
			public Size Size { get { return new Size(Width, Height); } }

			public Point Location { get { return new Point(Left, Top); } }

			// Handy method for converting to a System.Drawing.Rectangle
			public Rectangle ToRectangle() 
			{ return Rectangle.FromLTRB(Left, Top, Right, Bottom); }

			public static RECT FromRectangle(Rectangle rectangle) 
			{
				return new RECT(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom);
			}
		}

		[StructLayout(LayoutKind.Sequential)]
		struct TBBUTTON
		{
			public int iBitmap;
			public int idCommand;
			public byte fsState;
			public byte fsStyle;
			byte bReserved0; // padding for alignment
			byte bReserved1;
			public int dwData;
			public IntPtr iString;
		};

		const int PROCESS_ALL_ACCESS = 0x1f0fff;
		const uint TB_GETBUTTON = 1047;
		const uint TB_BUTTONCOUNT = 1048;
		const uint TB_GETITEMRECT = 1053;
		const int MEM_COMMIT = 0x00001000;
		const int PAGE_READWRITE = 4;
		const byte TBSTATE_HIDDEN = 8;
		const uint MEM_RELEASE = 0x8000;

		delegate int EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

		[DllImport("user32.dll")]
		static extern bool IsWindow(IntPtr hWnd);
		[DllImport("user32.dll")]
		static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
		[DllImport("user32.dll")]
		static extern bool EnumChildWindows(IntPtr hwndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);
		[DllImport("user32.dll")]
		static extern int GetClassName(IntPtr hWnd, [Out] StringBuilder lpClassName, int nMaxCount);
		[DllImport("user32.dll", SetLastError=true)]
		static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
		[DllImport("kernel32.dll")]
		static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);
		[DllImport("user32.dll")]
		static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
		[DllImport("kernel32.dll")]
		static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize, uint flAllocationType, uint flProtect);
//		[DllImport("kernel32.dll")]
//		static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte [] lpBuffer, UIntPtr nSize, IntPtr lpNumberOfBytesRead);
		[DllImport("kernel32.dll")]
		static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, IntPtr lpBuffer, UIntPtr nSize, IntPtr lpNumberOfBytesRead);
		[DllImport("user32.dll")]
		static extern int MapWindowPoints(IntPtr hwndFrom, IntPtr hwndTo, IntPtr lpPoints, uint cPoints);
		[DllImport("kernel32.dll")]
		static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize, uint dwFreeType);
		[DllImport("kernel32.dll", SetLastError=true)]
		static extern bool CloseHandle(IntPtr hObject);

		#endregion

		#region static unsafe int FindTrayHwnd(IntPtr hwnd, IntPtr lParam)

		static unsafe int FindTrayHwnd(IntPtr hwnd, IntPtr lParam)
		{
			StringBuilder className = new StringBuilder();
			GetClassName(hwnd, className, 255);

			// Did we find the Main System Tray? If so, then get its size and quit
			if(className.ToString() == "TrayNotifyWnd")
			{
				IntPtr* pWnd = (IntPtr*)lParam;
				*pWnd = hwnd;

				return 0;
			}
	
			 // Original code I found on Internet were seeking here for system clock and it was assumming that clock is on the right side of tray.
			 // After that calculated size of tray was adjusted by removing space occupied by clock.
			 // This is not a good idea - some clocks are ABOVE or somewhere else on the screen. I found that is far safer to just ignore clock space.

			return 1;
		}

		#endregion

		#region static unsafe int FindToolBarHwnd(IntPtr hwnd, IntPtr lParam)

		static unsafe int FindToolBarHwnd(IntPtr hwnd, IntPtr lParam)
		{
			StringBuilder className = new StringBuilder();
			GetClassName(hwnd, className, 255);

			// Did we find the Main System Tray? If so, then get its size and quit
			if(className.ToString() == "ToolbarWindow32")
			{
				IntPtr* pWnd = (IntPtr*)lParam;
				*pWnd = hwnd;

				return 0;
			}

			return 1;
		}

		#endregion

		#region static unsafe IntPtr GetTrayHwnd()

		static unsafe IntPtr GetTrayHwnd()
		{
			IntPtr trayHwnd = IntPtr.Zero;
			IntPtr shellTrayHwnd = FindWindow("Shell_TrayWnd", null);
			if(shellTrayHwnd != IntPtr.Zero)
			{
				EnumChildWindows(shellTrayHwnd, new EnumWindowsProc(FindTrayHwnd), new IntPtr(&trayHwnd));

				if(trayHwnd != IntPtr.Zero && IsWindow(trayHwnd))
				{
					IntPtr toolBarHwnd = IntPtr.Zero;
					EnumChildWindows(trayHwnd, new EnumWindowsProc(FindToolBarHwnd), new IntPtr(&toolBarHwnd));
					if(toolBarHwnd != IntPtr.Zero)
					{
						return toolBarHwnd;
					}
				}

				return trayHwnd;
			}

			return shellTrayHwnd;
		}

		#endregion

		#region public static unsafe bool FindIcon(IntPtr ownerHwnd, int iconId, out Rectangle iconBounds)

		public static unsafe bool FindIcon(IntPtr ownerHwnd, int iconId, out Rectangle iconBounds)
		{
			iconBounds = Rectangle.Empty;
			IntPtr trayHwnd = GetTrayHwnd();

			// now we have to get an ID of the parent process for system tray
			uint trayPid = 0;
			GetWindowThreadProcessId(trayHwnd, out trayPid);

			// here we get a handle to tray application process
			IntPtr trayProcessHandle = OpenProcess(PROCESS_ALL_ACCESS, false, trayPid);

			// now we check how many buttons is there - should be more than 0
			IntPtr buttonCount = SendMessage(trayHwnd, TB_BUTTONCOUNT, IntPtr.Zero, IntPtr.Zero);

			// We want to get data from another process - it's not possible
			// to just send messages like TB_GETBUTTON with a locally
			// allocated buffer for return data. Pointer to locally allocated
			// data has no usefull meaning in a context of another
			// process (since Win95) - so we need
			// to allocate some memory inside Tray process.
			// We allocate sizeof(TBBUTTON) bytes of memory -
			// because TBBUTTON is the biggest structure we will fetch.
			// But this buffer will be also used to get smaller
			// pieces of data like RECT structures.
			void* data = VirtualAllocEx(trayProcessHandle, IntPtr.Zero, new UIntPtr((uint)sizeof(TBBUTTON)), MEM_COMMIT, PAGE_READWRITE).ToPointer();

			bool iconFound = false;

			for(int i = 0; i < buttonCount.ToInt32(); i++)
			{
				// first let's read TBUTTON information
				// about each button in a task bar of tray
				int bytesRead = -1;
				TBBUTTON buttonData;
				SendMessage(trayHwnd, TB_GETBUTTON, new IntPtr(i), new IntPtr(data));

				// we filled lpData with details of iButton icon of toolbar
				// - now let's copy this data from tray application
				// back to our process
				
				ReadProcessMemory(trayProcessHandle, new IntPtr(data), new IntPtr(&buttonData), new UIntPtr((uint)sizeof(TBBUTTON)), new IntPtr(&bytesRead));

				// let's read extra data of each button:
				// there will be a HWND of the window that
				// created an icon and icon ID
				int[] extraData = { 0, 0 };
				fixed(void* pExtraData = extraData)
				{
					ReadProcessMemory(trayProcessHandle, new IntPtr((void*)buttonData.dwData), new IntPtr(pExtraData), new UIntPtr((uint)(sizeof(int) * extraData.Length)), new IntPtr(&bytesRead));
				}

				if((IntPtr)extraData[0] != ownerHwnd
					|| (int)extraData[1] != iconId)
				{
					continue;
				}

				// we found our icon - in WinXP it could be hidden - let's check it
				if((buttonData.fsState & TBSTATE_HIDDEN) != 0)
				{
					break;
				}

				// now just ask a tool bar of rectangle of our icon
				RECT rcPosition;
				SendMessage(trayHwnd, TB_GETITEMRECT, new IntPtr(i), new IntPtr(data));
				ReadProcessMemory(trayProcessHandle, new IntPtr(data), new IntPtr(&rcPosition), new UIntPtr((uint)sizeof(RECT)), new IntPtr(&bytesRead));

				MapWindowPoints(trayHwnd, IntPtr.Zero, new IntPtr(&rcPosition), 2);
				iconBounds = rcPosition.ToRectangle();

				iconFound = true;

				break;
			}

			VirtualFreeEx(trayProcessHandle, new IntPtr(data), UIntPtr.Zero, MEM_RELEASE);
			CloseHandle(trayProcessHandle);

			return iconFound;
		}

		#endregion
	}
}
