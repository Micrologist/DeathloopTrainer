using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;

namespace VeninethTrainer
{
    public partial class MainWindow : Window
    {
        globalKeyboardHook kbHook = new globalKeyboardHook();
        Timer updateTimer;
        Process game;
        public bool hooked = false;
        //DeepPointer cheatManagerDP, capsuleDP, charMoveCompDP, playerControllerDP, playerCharacterDP, worldDP, gameModeDP, worldSettingsDP;
        DeepPointer sphereDP = new DeepPointer(0x02F6BA98, 0x0, 0xE8, 0x398, 0x0);
        DeepPointer gravityDP = new DeepPointer("PhysX3_x64.dll", 0x191434);
        DeepPointer characterControllerDP = new DeepPointer(0x02F6C030, 0x30, 0x0);
        DeepPointer worldSettingsDP = new DeepPointer(0x02F8AB60, 0x30, 0x240, 0x0);

        IntPtr xVelPtr, yVelPtr, zVelPtr, xPosPtr, yPosPtr, zPosPtr, gameSpeedPtr, gravityPtr, hLookPtr, vLookPtr;
        byte[] gravityCode = new byte[5] { 0xF3, 0x0F, 0x11, 0x69, 0x08 };
        byte[] gravityNop = new byte[5] { 0x90, 0x90, 0x90, 0x90, 0x90 };

        double forward, side = 0;


        private void gameSpeedBtn_Click(object sender, RoutedEventArgs e)
        {
            ChangeGameSpeed();
        }

        private void teleBtn_Click(object sender, RoutedEventArgs e)
        {
            Teleport();
        }

        private void saveBtn_Click(object sender, RoutedEventArgs e)
        {
            StorePosition();
        }

        private void noclipBtn_Click(object sender, RoutedEventArgs e)
        {
            ToggleNoclip();
        }


        float xVel, yVel, zVel, xPos, yPos, zPos, vLook, hLook, gameSpeed, prefGameSpeed;
        bool ghost, god, noclip;
        int charLFS;
        float[] storedPos = new float[5] { 0f, 0f, 0f, 0f, 0f };


        public MainWindow()
        {
            InitializeComponent();

            kbHook.KeyDown += InputKeyDown;
            kbHook.KeyUp += InputKeyUp;
            kbHook.HookedKeys.Add(System.Windows.Forms.Keys.F1);
            kbHook.HookedKeys.Add(System.Windows.Forms.Keys.F4);
            kbHook.HookedKeys.Add(System.Windows.Forms.Keys.F5);
            kbHook.HookedKeys.Add(System.Windows.Forms.Keys.F6);

            prefGameSpeed = 1.0f;

            updateTimer = new Timer
            {
                Interval = (16) // ~60 Hz
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
            double hVel = Math.Floor(Math.Sqrt(xVel * xVel + yVel * yVel) + 0.5f) / 100;

            game.ReadValue<float>(vLookPtr, out vLook);
            game.ReadValue<float>(hLookPtr, out hLook);

            game.ReadValue<float>(gameSpeedPtr, out gameSpeed);


            SetLabel(noclip, noclipLabel);

            SetGameSpeed();

            gameSpeedLabel.Content = prefGameSpeed.ToString("0.0") + "x";

            positionBlock.Text = (xPos / 100).ToString("0.00") + "\n" + (yPos / 100).ToString("0.00") + "\n" + (zPos / 100).ToString("0.00");
            speedBlock.Text = hVel.ToString("0.00") + " m/s";

            if (noclip)
            {
                double radian = (double)hLook * (Math.PI / 180);
                Vector direction = new Vector(Math.Cos(radian), Math.Sin(radian));
                Debug.WriteLine("{0}, {1}", forward, side);
                Vector inputVector = new Vector((double)side, (double)forward);
                if(inputVector.Length > 1)
                    inputVector.Normalize();

                Debug.WriteLine(inputVector);
                float newXVel = (float)direction.X * (float)inputVector.Y * 5000;
                float newYVel = (float)direction.Y * (float)inputVector.Y * 5000;

                Debug.WriteLine("XVEL {0}, YVEL {1}", newXVel, newYVel);


                radian = (double)vLook * (Math.PI / 180);
                direction = new Vector(Math.Cos(radian), Math.Sin(radian));
                Debug.WriteLine("up vector: " + direction);
                float newZVel = (float)(direction.Y * forward * 10000);
                game.WriteBytes(zVelPtr, BitConverter.GetBytes(newZVel));
                float nonUpMultiplier = 1f - Math.Abs((float)direction.Y);
                game.WriteBytes(xVelPtr, BitConverter.GetBytes(newXVel*nonUpMultiplier));
                game.WriteBytes(yVelPtr, BitConverter.GetBytes(newYVel*nonUpMultiplier));
            }
        }

        private bool Hook()
        {
            List<Process> processList = Process.GetProcesses().ToList().FindAll(x => x.ProcessName.Contains("Game-Win64-Shipping"));
            if (processList.Count == 0)
            {
                game = null;
                return false;
            }
            game = processList[0];

            if (game.HasExited)
                return false;

            return true;
        }



        private void DerefPointers()
        {
            sphereDP.DerefOffsets(game, out IntPtr basePtr);
            xPosPtr = basePtr + 0xA0;
            yPosPtr = basePtr + 0xA4;
            zPosPtr = basePtr + 0xA8;
            xVelPtr = basePtr + 0xD0;
            yVelPtr = basePtr + 0xD4;
            zVelPtr = basePtr + 0xD8;
            gravityDP.DerefOffsets(game, out gravityPtr);

            characterControllerDP.DerefOffsets(game, out IntPtr characterControllerPtr);
            vLookPtr = characterControllerPtr + 0x2A8;
            hLookPtr = characterControllerPtr + 0x2AC;


            worldSettingsDP.DerefOffsets(game, out IntPtr worldSettingsPtr);
            gameSpeedPtr = worldSettingsPtr + 0x308;
        }

        private void InputKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.F1:
                    ToggleNoclip();
                    break;
                case Keys.F4:
                    ChangeGameSpeed();
                    break;
                case Keys.F5:
                    StorePosition();
                    break;
                case Keys.F6:
                    Teleport();
                    break;
                case Keys.W:
                    forward = 1;
                    break;
                default:
                    break;
            }
            e.Handled = true;
        }

        private void ToggleNoclip()
        {
            noclip = !noclip;
            if (noclip)
            {
                game.WriteBytes(gravityPtr, gravityNop);
                kbHook.HookedKeys.Add(System.Windows.Forms.Keys.W);
            }
            else
            {
                kbHook.HookedKeys.Remove(System.Windows.Forms.Keys.W);
                game.WriteBytes(gravityPtr, gravityCode);
            }

        }


        private void Teleport()
        {
            game.WriteBytes(xPosPtr, BitConverter.GetBytes(storedPos[0]));
            game.WriteBytes(yPosPtr, BitConverter.GetBytes(storedPos[1]));
            game.WriteBytes(zPosPtr, BitConverter.GetBytes(storedPos[2]));

            game.WriteBytes(vLookPtr, BitConverter.GetBytes(storedPos[3]));
            game.WriteBytes(hLookPtr, BitConverter.GetBytes(storedPos[4]));

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
            storedPos = new float[5] { xPos, yPos, zPos, vLook, hLook };
        }

        private void InputKeyUp(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.W:
                    forward = 0;
                    break;
                case Keys.S:
                    forward = 0;
                    break;
                case Keys.A:
                    side = 0;
                    break;
                case Keys.D:
                    side = 0;
                    break;
            }
            e.Handled = true;
        }

        private void ChangeGameSpeed()
        {
            switch (prefGameSpeed)
            {
                case 1.0f:
                    prefGameSpeed = 2.0f;
                    break;
                case 2.0f:
                    prefGameSpeed = 4.0f;
                    break;
                case 4.0f:
                    prefGameSpeed = 0.5f;
                    break;
                case 0.5f:
                    prefGameSpeed = 1.0f;
                    break;
                default:
                    prefGameSpeed = 1.0f;
                    break;
            }
        }

        private void SetGameSpeed()
        {
            Debug.WriteLine("{0} want {1}", gameSpeed, prefGameSpeed);
            if ((gameSpeed == 1.0f || gameSpeed == 2.0f || gameSpeed == 4.0f || gameSpeed == 0.5f) && gameSpeed != prefGameSpeed)
                game.WriteBytes(gameSpeedPtr, BitConverter.GetBytes(prefGameSpeed));
        }
    }
}
