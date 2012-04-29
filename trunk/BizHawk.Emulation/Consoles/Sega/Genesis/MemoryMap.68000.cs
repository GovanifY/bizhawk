﻿using System;

namespace BizHawk.Emulation.Consoles.Sega
{
    partial class Genesis
    {
        public sbyte ReadByte(int address)
        {
            address &= 0x00FFFFFF;

            if (address < 0x400000)
                return (sbyte) RomData[address];

            if (address >= 0xE00000)
                return (sbyte) Ram[address & 0xFFFF];

            if (address == 0xA11100) // Z80 BUS status
            {
                //Console.WriteLine("QUERY z80 bus status. 68000 can access? " + (M68000HasZ80Bus && Z80Reset == false));
                return (sbyte) (M68000HasZ80Bus && Z80Reset == false ? 0 : 1);
            }

            if (address >= 0xA10000 && address <= 0xA1001F)
                return (sbyte)ReadIO(address);

            if ((address & 0xFF0000) == 0xA00000)
                return (sbyte) ReadMemoryZ80((ushort) (address & 0x7FFF));

            if (address >= 0xC00004 && address < 0xC00008)
            {
                ushort value = VDP.ReadVdpControl();
                if ((address & 1) == 0) // read MSB
                    return (sbyte) (value >> 8);
                return (sbyte) value; // read LSB
            }

            Console.WriteLine("UNHANDLED READB {0:X6}", address);
            return 0x7D;
        }
 
        public short ReadWord(int address)
        {
            address &= 0x00FFFFFF;

            int maskedAddr;
            if (address < 0x400000)
                return (short)((RomData[address] << 8) | RomData[address + 1]);

            if (address >= 0xE00000) // Work RAM
            {
                maskedAddr = address & 0xFFFF;
                return (short)((Ram[maskedAddr] << 8) | Ram[maskedAddr + 1]);
            }

            if (address >= 0xC00004 && address < 0xC00008)
                return (short) VDP.ReadVdpControl();

            if (address >= 0xA10000 && address <= 0xA1001F)
                return (sbyte)ReadIO(address);

            Console.WriteLine("UNHANDLED READW {0:X6}", address);
            return 0x7DCD;
        }

        public int ReadLong(int address)
        {
            address &= 0x00FFFFFF;

            int maskedAddr;
            if (address < 0x400000) // Cartridge ROM
                return (RomData[address] << 24) | (RomData[address + 1] << 16) | (RomData[address + 2] << 8) | RomData[address + 3];

            if (address >= 0xE00000) // Work RAM
            {
                maskedAddr = address & 0xFFFF;
                return (Ram[maskedAddr] << 24) | (Ram[maskedAddr + 1] << 16) | (Ram[maskedAddr + 2] << 8) | Ram[maskedAddr + 3];
            }

            // try to handle certain things separate if they need to be separate? otherwise handle as 2x readwords?
            {
                return ((ushort)ReadWord(address) | (ushort)(ReadWord(address + 2) << 16));
            }
            //if (address == 0xA10008) return 0; // FIXME HACK for tg-sync.

            //Console.WriteLine("UNHANDLED READL {0:X6}", address);
            //return 0x7DCDCDCD;
        }

        public void WriteByte(int address, sbyte value)
        {
            address &= 0x00FFFFFF;

            if (address >= 0xE00000) // Work RAM
            {
                //Console.WriteLine("MEM[{0:X4}] change from {1:X2} to {2:X2}", address & 0xFFFF, Ram[address & 0xFFFF], value);
                Ram[address & 0xFFFF] = (byte)value;
                return;
            }
            if ((address & 0xFF0000) == 0xA00000)
            {
                WriteMemoryZ80((ushort)(address & 0x7FFF), (byte)value);
                return;
            }
            if (address >= 0xA10000 && address <= 0xA1001F)
            {
                WriteIO(address, value); 
                return;
            }
            if (address == 0xA11100)
            {
                M68000HasZ80Bus = (value & 1) != 0;
                //Console.WriteLine("68000 has the z80 bus: " + M68000HasZ80Bus);
                return;
            }
            if (address == 0xA11200) // Z80 RESET
            {
                Z80Reset = (value & 1) == 0;
                if (Z80Reset)
                    SoundCPU.Reset();
                //Console.WriteLine("z80 reset: " + Z80Reset);
                return;
            }
            if (address >= 0xC00000)
            {
                // when writing to VDP in byte mode, the LSB is duplicated into the MSB
                switch (address & 0x1F)
                {
                    case 0x00:
                    case 0x02:
                        //zero 15-feb-2012 -  added latter two ushort casts to kill a warning
                        VDP.WriteVdpData((ushort) ((ushort)value | ((ushort)value << 8)));
                        return;
                    case 0x04:
                    case 0x06:
                        //zero 15-feb-2012 -  added latter two ushort casts to kill a warning
                        VDP.WriteVdpControl((ushort) ((ushort)value | ((ushort)value << 8)));
                        return;
                    case 0x11:
                    case 0x13:
                    case 0x15:
                    case 0x17:
                        PSG.WritePsgData((byte) value, SoundCPU.TotalExecutedCycles);
                        return;
                }
            }

            Console.WriteLine("UNHANDLED WRITEB {0:X6}:{1:X2}", address, value);
        }

        public void WriteWord(int address, short value)
        {
            address &= 0x00FFFFFF;

            if (address >= 0xE00000) // Work RAM
            {
                //Console.WriteLine("MEM[{0:X4}] change to {1:X4}", address & 0xFFFF, value);
                Ram[(address & 0xFFFF) + 0] = (byte)(value >> 8);
                Ram[(address & 0xFFFF) + 1] = (byte)value;
                return;
            }
            if (address >= 0xC00000)
            {
                switch (address & 0x1F)
                {
                    case 0x00:
                    case 0x02:
                        VDP.WriteVdpData((ushort)value);
                        return;
                    case 0x04:
                    case 0x06:
                        VDP.WriteVdpControl((ushort)value);
                        return;
                }
            }
            if (address == 0xA11100) // Z80 BUSREQ
            {
                M68000HasZ80Bus = (value & 0x100) != 0;
                //Console.WriteLine("68000 has the z80 bus: " + M68000HasZ80Bus);
                return;
            }
            if (address == 0xA11200) // Z80 RESET
            {
                Z80Reset = (value & 0x100) == 0;
                if (Z80Reset)
                    SoundCPU.Reset();
                //Console.WriteLine("z80 reset: " + Z80Reset);
                return;
            }
            Console.WriteLine("UNHANDLED WRITEW {0:X6}:{1:X4}", address, value);
        }

        public void WriteLong(int address, int value)
        {
            address &= 0x00FFFFFF;

            if (address >= 0xE00000) // Work RAM
            {
                //Console.WriteLine("MEM[{0:X4}] change to {1:X8}", address & 0xFFFF, value);
                Ram[(address & 0xFFFF) + 0] = (byte)(value >> 24);
                Ram[(address & 0xFFFF) + 1] = (byte)(value >> 16);
                Ram[(address & 0xFFFF) + 2] = (byte)(value >> 8);
                Ram[(address & 0xFFFF) + 3] = (byte)value;
                return;
            }
            if (address >= 0xC00000)
            {
                WriteWord(address, (short)(value >> 16));
                WriteWord(address, (short)value);
                return;
            }

            Console.WriteLine("UNHANDLED WRITEL {0:X6}:{1:X8}", address, value);
        }
    }
}
