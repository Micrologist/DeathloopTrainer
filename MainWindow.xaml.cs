using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;

namespace DeathloopTrainer
{
	public partial class MainWindow : Window
	{

		globalKeyboardHook kbHook = new globalKeyboardHook();
		Timer updateTimer;
		Process game;
		public bool hooked = false;

		DeepPointer characterDP = new DeepPointer(0x02D5F688, 0x8, 0x8, 0x98, 0xA0, 0x1F0, 0x0);
		DeepPointer rotationDP = new DeepPointer(0x810, 0x0);
		DeepPointer statusDP = new DeepPointer(0x900);

		IntPtr xVelPtr, yVelPtr, zVelPtr, xPosPtr, yPosPtr, zPosPtr, godPtr, ammoPtr, rotAPtr, rotBPtr;

		bool god, ammo, teleFw, teleUp = false;
		float[] storedPos = new float[5] { 0f, 0f, 0f, 0f, 0f };


		float xVel, yVel, zVel, xPos, yPos, zPos, rotA, rotB;

		private void teleportFwBtn_Click(object sender, RoutedEventArgs e)
		{
			e.Handled = true;
			TeleportForward();
		}

		private void godModeBtn_Click(object sender, RoutedEventArgs e)
		{
			e.Handled = true;
			ToggleGod();
		}

		private void ammoBtn_Click(object sender, RoutedEventArgs e)
		{
			e.Handled = true;
			ToggleAmmo();
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			TestFunction();
		}

		private void teleUpBtn_Click(object sender, RoutedEventArgs e)
		{
			e.Handled = true;
			TeleportUpward();
		}

		private void saveBtn_Click(object sender, RoutedEventArgs e)
		{
			e.Handled = true;
			StorePosition();
		}

		private void teleBtn_Click(object sender, RoutedEventArgs e)
		{
			e.Handled = true;
			Teleport();
		}

		public MainWindow()
		{
			InitializeComponent();
			kbHook.KeyDown += InputKeyDown;
			kbHook.KeyUp += InputKeyUp;
			kbHook.HookedKeys.Add(System.Windows.Forms.Keys.F1);
			kbHook.HookedKeys.Add(System.Windows.Forms.Keys.F2);
			kbHook.HookedKeys.Add(System.Windows.Forms.Keys.F3);
			kbHook.HookedKeys.Add(System.Windows.Forms.Keys.F5);
			kbHook.HookedKeys.Add(System.Windows.Forms.Keys.F6);

			updateTimer = new Timer
			{
				Interval = 16 // ~60 Hz
			};
			updateTimer.Tick += new EventHandler(Update);
			updateTimer.Start();
		}

		private void Update(object sender, EventArgs e)
		{
			if (game == null || game.HasExited)
			{
				game = null;
				hooked = false;
			}
			if (!hooked)
				hooked = Hook();
			if (!hooked)
				return;
			try
			{
				DerefPointers();
			}
			catch (Exception)
			{
				return;
			}


			game.ReadValue<float>(xPosPtr, out xPos);
			game.ReadValue<float>(yPosPtr, out yPos);
			game.ReadValue<float>(zPosPtr, out zPos);

			game.ReadValue<float>(xVelPtr, out xVel);
			game.ReadValue<float>(yVelPtr, out yVel);
			game.ReadValue<float>(zVelPtr, out zVel);
			double hVel = (Math.Sqrt(xVel * xVel + yVel * yVel));

			game.ReadValue<float>(rotAPtr, out rotA);
			game.ReadValue<float>(rotBPtr, out rotB);

			game.ReadValue<bool>(godPtr, out god);
			game.ReadValue<bool>(ammoPtr, out ammo);


			SetLabel(god, godModeLabel);
			SetLabel(ammo, ammoLabel);


			positionBlock.Text = (xPos).ToString("0.00") + "\n" + (yPos).ToString("0.00") + "\n" + (zPos).ToString("0.00");
			speedBlock.Text = hVel.ToString("0.00") + " m/s";
			if (teleFw)
			{
				TeleportForward();
			}
			if (teleUp)
			{
				TeleportUpward();
			}
		}

		private bool Hook()
		{
			List<Process> processList = Process.GetProcesses().ToList().FindAll(x => x.ProcessName == "Deathloop");
			if (processList.Count == 0)
			{
				game = null;
				return false;
			}
			game = processList[0];

			if (game.HasExited)
				return false;

			try
			{
				if (game.Modules.Count < 110)
					return false;
			}
			catch
			{
				return false;
			}

			if (new DeepPointer(0x900).Deref<byte>(game, out byte statusByte))
			{
				Debug.WriteLine(statusByte);
				if (statusByte != (byte)0x1)
				{
					bool success = false;
					while (!success)
					{
						Debug.WriteLine("attempting to write function hooks");
						success = WriteFunctionHooks();
					}
				}
				return true;
			}
			return false;
		}



		private void DerefPointers()
		{
			characterDP.DerefOffsets(game, out IntPtr basePtr);
			xPosPtr = basePtr + 0x80;
			yPosPtr = basePtr + 0x84;
			zPosPtr = basePtr + 0x88;
			xVelPtr = basePtr + 0xB0;
			yVelPtr = basePtr + 0xB4;
			zVelPtr = basePtr + 0xB8;

			statusDP.DerefOffsets(game, out IntPtr statusBasePtr);
			godPtr = statusBasePtr + 0x10;
			ammoPtr = statusBasePtr + 0x20;

			rotationDP.DerefOffsets(game, out basePtr);
			rotAPtr = basePtr + 0x1B4;
			rotBPtr = basePtr + 0x1B8;

		}

		private void InputKeyDown(object sender, KeyEventArgs e)
		{
			switch (e.KeyCode)
			{
				case Keys.F1:
					teleFw = true;
					break;
				case Keys.F2:
					teleUp = true;
					break;
				case Keys.F3:
					ToggleGod();
					break;
				case Keys.F4:
					ToggleAmmo();
					break;
				case Keys.F5:
					StorePosition();
					break;
				case Keys.F6:
					Teleport();
					break;
				default:
					break;
			}
			e.Handled = true;
		}

		private void ToggleAmmo()
		{
			if (!hooked)
				return;

			game.WriteValue<bool>(ammoPtr, !ammo);
		}

		private void ToggleGod()
		{
			if (!hooked)
				return;

			game.WriteValue<bool>(godPtr, !god);
		}

		private void TeleportForward()
		{
			if (!hooked)
				return;

			System.Windows.Media.Media3D.Quaternion q = new System.Windows.Media.Media3D.Quaternion(0, rotB, 0, rotA);
			float angle = ((float)((q.Angle / 2) * -q.Axis.Y));
			angle = (float)(angle / 180 * Math.PI);

			double x = 0, y = 0;
			x += Math.Sin(angle + Math.PI / 2) * 1;
			y += Math.Cos(angle + Math.PI / 2) * 1;
			Debug.WriteLine(x + ", " + y);
			float scale = 1f;

			game.WriteBytes(xPosPtr, BitConverter.GetBytes((float)(xPos + (float)(x * scale * 1))));
			game.WriteBytes(yPosPtr, BitConverter.GetBytes((float)(yPos + (float)(y * scale * 1))));
		}

		private void TeleportUpward()
		{
			if (!hooked)
				return;


			float scale = 1f;
			if(zVel < 0.0f)
			{
				scale = Math.Abs(zVel) / 10;
			}

			game.WriteBytes(zPosPtr, BitConverter.GetBytes((float)(zPos + (float)(scale * 1))));

		}

		private void Teleport()
		{
			if (!hooked)
				return;
			game.WriteBytes(xPosPtr, BitConverter.GetBytes(storedPos[0]));
			game.WriteBytes(yPosPtr, BitConverter.GetBytes(storedPos[1]));
			game.WriteBytes(zPosPtr, BitConverter.GetBytes(storedPos[2]));

			game.WriteBytes(xVelPtr, BitConverter.GetBytes(0f));
			game.WriteBytes(yVelPtr, BitConverter.GetBytes(0f));
			game.WriteBytes(zVelPtr, BitConverter.GetBytes(0f));
		}

		private void SetLabel(bool state, System.Windows.Controls.Label label)
		{
			if (state)
			{
				label.Content = "ON";
				label.Foreground = Brushes.Green;
			}
			else
			{
				label.Content = "OFF";
				label.Foreground = Brushes.Red;
			}
		}

		private void StorePosition()
		{
			if (!hooked)
				return;
			storedPos = new float[3] { xPos, yPos, zPos };
			return;
		}

		private void InputKeyUp(object sender, KeyEventArgs e)
		{
			switch (e.KeyCode)
			{
				case Keys.F1:
					teleFw = false;
					break;
				case Keys.F2:
					teleUp = false;
					break;
			}
			e.Handled = true;
		}

		private bool WriteFunctionHooks()
		{
			SignatureScanner scanner = new SignatureScanner(game, game.MainModule.BaseAddress, (int)game.MainModule.ModuleMemorySize);

			SigScanTarget antiDebugSig = new SigScanTarget("FF 50 38 88 44 24 70 E8 ?? ?? ?? ?? 0F B6 C0 83 F8 01 0F 85 ?? ?? ?? ?? C7 84 24 F8 01 00 00 3C 00 00 00");
			IntPtr antiDbgPtr = scanner.Scan(antiDebugSig);
			if (antiDbgPtr != IntPtr.Zero)
			{
				game.WriteBytes(antiDbgPtr, StringToByteArray("B0 01 90"));
			}


			SigScanTarget getPlayerSig = new SigScanTarget("?? ?? ?? ?? ?? 48 83 C4 20 5F C3 83 FB 03");

			IntPtr getPlayerPtr = IntPtr.Zero;
			getPlayerPtr = scanner.Scan(getPlayerSig);
			if (getPlayerPtr == IntPtr.Zero)
			{
				throw new Exception("Can't Find GetPlayer Signature");
			}

			_ = new DeepPointer(0x900).DerefOffsets(game, out IntPtr statusPtr);
			IntPtr godModePtr = statusPtr + 0x10;
			IntPtr ammoInfPtr = statusPtr + 0x20;
			_ = new DeepPointer(0x500).DerefOffsets(game, out IntPtr newGetPlayerPtr);
			_ = new DeepPointer(0x800).DerefOffsets(game, out IntPtr playerPtr);
			byte[] newGetPlayerCode = StringToByteArray("48 A3 " + IntPtrToASMString(playerPtr)
				+ " 48 8B 74 24 40");

			game.WriteBytes(newGetPlayerPtr, newGetPlayerCode);

			long jumpOffset = getPlayerPtr.ToInt64() - newGetPlayerPtr.ToInt64() - newGetPlayerCode.Length;
			byte[] jumpCode = StringToByteArray("E9 " + IntPtrToASMString(new IntPtr(jumpOffset), 4));
			game.WriteBytes(newGetPlayerPtr + newGetPlayerCode.Length, jumpCode);

			jumpOffset = newGetPlayerPtr.ToInt64() - getPlayerPtr.ToInt64() - 0x5;
			jumpCode = jumpCode = StringToByteArray("E9 " + IntPtrToASMString(new IntPtr((uint)jumpOffset), 4));

			game.WriteBytes(getPlayerPtr, jumpCode);


			SigScanTarget applyDamageSig = new SigScanTarget("?? ?? ?? ?? ?? ?? ?? 0F 28 C1 75 ?? 80 B9");
			IntPtr applyDamagePtr = scanner.Scan(applyDamageSig);
			if (applyDamagePtr == IntPtr.Zero)
			{
				throw new Exception("Can't Find ApplyDamage Signature");
			}

			_ = new DeepPointer(0x550).DerefOffsets(game, out IntPtr newApplyDamagePtr);
			jumpOffset = godModePtr.ToInt64() - newApplyDamagePtr.ToInt64() - 0x9;
			byte[] newApplyDamageCode = StringToByteArray("53 52 48 8B 1D " + IntPtrToASMString(new IntPtr(jumpOffset), 4) +
				" 48 83 FB 01 75 11 48 8B 59 70 48 BA " + IntPtrToASMString(playerPtr) +
				" 48 3B 1A 5A 5B 75 01 C3 80 B9 18 04 00 00 00");
			game.WriteBytes(newApplyDamagePtr, newApplyDamageCode);

			jumpOffset = applyDamagePtr.ToInt64() - newApplyDamagePtr.ToInt64() - newApplyDamageCode.Length + 2;
			jumpCode = StringToByteArray("E9 " + IntPtrToASMString(new IntPtr(jumpOffset), 4));
			game.WriteBytes(newApplyDamagePtr + newApplyDamageCode.Length, jumpCode);

			jumpOffset = newApplyDamagePtr.ToInt64() - applyDamagePtr.ToInt64() - 0x5;
			jumpCode = StringToByteArray("E9 " + IntPtrToASMString(new IntPtr((uint)jumpOffset), 4) + " 66 90");
			game.WriteBytes(applyDamagePtr, jumpCode);


			SigScanTarget applyWaterDamageSig = new SigScanTarget("?? ?? ?? ?? ?? 57 48 83 EC ?? 80 B9 ?? ?? ?? ?? 00 41 0F B6 F8 0F 29");
			IntPtr applyWaterDamagePtr = scanner.Scan(applyWaterDamageSig);
			if (applyWaterDamagePtr == IntPtr.Zero)
			{
				throw new Exception("Can't Find GetPlayer Signature");
			}

			_ = new DeepPointer(0x600).DerefOffsets(game, out IntPtr newApplyWaterDamagePtr);
			jumpOffset = godModePtr.ToInt64() - newApplyWaterDamagePtr.ToInt64() - 0x9;
			byte[] newApplyWaterDamageCode = StringToByteArray("53 52 48 8B 1D " + IntPtrToASMString(new IntPtr(jumpOffset), 4) +
				" 48 83 FB 01 75 11 48 8B 59 70 48 BA " + IntPtrToASMString(playerPtr) +
				" 48 3B 1A 5A 5B 75 01 C3 48 89 5C 24 08");
			game.WriteBytes(newApplyWaterDamagePtr, newApplyWaterDamageCode);

			jumpOffset = applyWaterDamagePtr.ToInt64() - newApplyWaterDamagePtr.ToInt64() - newApplyWaterDamageCode.Length;
			jumpCode = StringToByteArray("E9 " + IntPtrToASMString(new IntPtr(jumpOffset), 4));
			game.WriteBytes(newApplyWaterDamagePtr + newApplyWaterDamageCode.Length, jumpCode);

			jumpOffset = newApplyWaterDamagePtr.ToInt64() - applyWaterDamagePtr.ToInt64() - 0x5;
			jumpCode = StringToByteArray("E9 " + IntPtrToASMString(new IntPtr((uint)jumpOffset), 4));
			game.WriteBytes(applyWaterDamagePtr, jumpCode);


			SigScanTarget consumeAmmoSig = new SigScanTarget("?? ?? ?? ?? ?? 48 C7 44 24 ?? FE FF FF FF 48 89 5C 24 ?? 48 89 74 24 ?? 48 8B D9 44 8B 81");
			IntPtr consumeAmmoPtr = scanner.Scan(consumeAmmoSig);
			if (consumeAmmoPtr == IntPtr.Zero)
			{
				throw new Exception("Can't Find ConsumeAmmo Signature");
			}

			_ = new DeepPointer(0x650).DerefOffsets(game, out IntPtr newConsumeAmmoPtr);
			jumpOffset = ammoInfPtr.ToInt64() - newConsumeAmmoPtr.ToInt64() - 0x9;

			byte[] newConsumeAmmoCode = StringToByteArray("53 52 48 8B 1D " + IntPtrToASMString(new IntPtr(jumpOffset), 4) +
				" 48 83 FB 01 75 18 48 8B 99 08 01 00 00 48 8B 5B 70 48 BA " + IntPtrToASMString(playerPtr) +
				" 48 3B 1A 5A 5B 75 01 C3 40 57 48 83 EC 40");
			game.WriteBytes(newConsumeAmmoPtr, newConsumeAmmoCode);

			jumpOffset = consumeAmmoPtr.ToInt64() - newConsumeAmmoPtr.ToInt64() - newConsumeAmmoCode.Length;
			jumpCode = StringToByteArray("E9 " + IntPtrToASMString(new IntPtr(jumpOffset), 4));
			game.WriteBytes(newConsumeAmmoPtr + newConsumeAmmoCode.Length, jumpCode);

			jumpOffset = newConsumeAmmoPtr.ToInt64() - consumeAmmoPtr.ToInt64() - 0x5;
			jumpCode = StringToByteArray("E9 " + IntPtrToASMString(new IntPtr((uint)jumpOffset), 4) + " 90");
			game.WriteBytes(consumeAmmoPtr, jumpCode);

			SigScanTarget rotationTarget = new SigScanTarget("48 89 5D 28 48 8B D3 48 8D 0D ?? ?? ?? ??");
			IntPtr ptr = scanner.Scan(rotationTarget) + 0xA;
			ptr += game.ReadValue<int>(ptr) + 0x4;
			_ = new DeepPointer(0x810).DerefOffsets(game, out IntPtr newRotPtr);
			game.WriteBytes(newRotPtr, BitConverter.GetBytes(ptr.ToInt64()));

			game.WriteValue<byte>(statusPtr, 0x1);
			return true;
		}


		byte[] StringToByteArray(string input)
		{
			string[] byteStringArray = input.Split(' ');
			byte[] output = new byte[byteStringArray.Length];
			for (int i = 0; i < byteStringArray.Length; i++)
			{
				output[i] = byte.Parse(byteStringArray[i], System.Globalization.NumberStyles.HexNumber);
			}
			return output;
		}

		string IntPtrToASMString(IntPtr input, int length = 8)
		{
			string ptrString = input.ToString("X16");
			string output = "";
			for (int i = 0; i < length; i++)
			{
				output += ptrString[14 - i * 2];
				output += ptrString[15 - i * 2];
				if (i != length - 1)
					output += " ";
			}
			return output;
		}


		void TestFunction()
		{
			SignatureScanner scanner = new SignatureScanner(game, game.MainModule.BaseAddress, game.MainModule.ModuleMemorySize);
			/*
			Deathloop.exe+C0CDF5 - 42 89 04 37           - mov [rdi+r14],eax
			Deathloop.exe+C0CDF9 - 48 83 C3 20           - add rbx,20
			Deathloop.exe+C0CDFD - 48 89 5D 28           - mov [rbp+28],rbx
			Deathloop.exe+C0CE01 - 48 8B D3              - mov rdx,rbx
			Deathloop.exe+C0CE04 - 48 8D 0D A5616702     - lea rcx,[Deathloop.exe+3282FB0] <---
			*/
			SigScanTarget rotationTarget = new SigScanTarget("48 89 5D 28 48 8B D3 48 8D 0D ?? ?? ?? ??");
			IntPtr ptr = scanner.Scan(rotationTarget) + 0xA;
			ptr += game.ReadValue<int>(ptr) + 0x4;
			_ = new DeepPointer(0x810).DerefOffsets(game, out IntPtr newRotPtr);
			game.WriteBytes(newRotPtr, BitConverter.GetBytes(ptr.ToInt64()));
		}

	}
}