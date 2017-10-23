using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pGameBoy
{
    class Cpu
    {
        private Core _core;

        private int cycles;
        ushort opcode;

        // Registers

        enum Registers
        {
            A, B, C, D, E, F, H, L,
            AF, BC, DE, HL,
            PC, SP,
            n, nn // n and nn is used for 8/18bit immidiate load in instruction.
        }
        enum Flags
        {
            ZeroFlag,
            SubFlag,
            HalfCarry,
            Carry
        }

        private ushort sp;
        private ushort pc;

        private byte a;
        private byte b;
        private byte c;
        private byte d;
        private byte e;
        private byte f;
        private byte h;
        private byte l;

        private ushort af { get { return (ushort)(a << 8 | f); } set { a = (byte)(value >> 8); f = (byte)(value & 0xFF); } }
        private ushort bc { get { return (ushort)(b << 8 | c); } set { b = (byte)(value >> 8); c = (byte)(value & 0xFF); } }
        private ushort de { get { return (ushort)(d << 8 | e); } set { d = (byte)(value >> 8); e = (byte)(value & 0xFF); } }
        private ushort hl { get { return (ushort)(h << 8 | l); } set { h = (byte)(value >> 8); l = (byte)(value & 0xFF); } }


        public int Cycles { get { return cycles; } }
        public bool InteruptsEnabled { get { return interuptsenabled; } }
        public bool Halt { get { return halt; } set { halt = value; } }

        const byte zeroflag = 0x80;
        const byte subtractflag = 0x40;
        const byte halfcarry = 0x20;
        const byte carry = 0x10;

        //bool
        private bool halt = false;
        private bool stop = false;
        private bool interuptsenabled = false;


        public Cpu(Core core)
        {
            _core = core;
        }


        public void Cycle()
        {
            if (!halt) ExecuteInstruction();
            else cycles += 4;
        }

        public void Interrupt(ushort interruptvector)
        {
            cycles += 20;
            interuptsenabled = false;
            Push_PC();
            SetReg(Registers.PC, interruptvector);
        }
        public void ResetCpu()
        {
            af = 0x11B0;
            bc = 0x0013;
            de = 0x00D8;
            hl = 0x014D;
            pc = 0x0100;
            sp = 0xFFFE;
            cycles = 0;
            interuptsenabled = false;
        }
        private ushort Imm8bitvalue()
        {
            return _core.ReadMem(pc++);
        }
        private ushort Imm16bitvalue()
        {
            return (ushort)(Imm8bitvalue() | Imm8bitvalue() << 8);
        }
        private byte GetFlag(Flags flag)
        {
            switch (flag)
            {
                case Flags.Carry: return (byte)((f & carry) != 0 ? 1 : 0);
                case Flags.HalfCarry: return (byte)((f & halfcarry) != 0 ? 1 : 0);
                case Flags.ZeroFlag: return (byte)((f & zeroflag) != 0 ? 1 : 0);
                case Flags.SubFlag: return (byte)((f & subtractflag) != 0 ? 1 : 0);

                default: return 0;
            }
        }
        private void SetFlag(Flags flag, byte value)
        {
            switch (flag)
            {
                case Flags.Carry:
                    byte carrymask = carry;  //Cant cast ~carry to byte because reasons?? Need to define new byte
                    if (value != 0)
                    {
                        f |= carrymask;
                       
                    }
                    else
                    {
                        //f &= (byte)~carry; //Cant cast ~carry to byte because reasons?
                        f &= (byte)~carrymask;
                    }
                    break;
                case Flags.HalfCarry:
                    byte hcarrymask = halfcarry;
                    if (value != 0)
                    {
                        f |= hcarrymask;
                    }
                    else
                    {
                        f &= (byte)~hcarrymask;
                    }
                    break;
                case Flags.ZeroFlag:
                    byte zeromask = zeroflag;
                    if (value != 0)
                    {
                        f |= zeromask;
                    }
                    else
                    {
                        f &= (byte)~zeromask;
                    }
                    break;
                case Flags.SubFlag:
                    byte submask = subtractflag;
                    if (value != 0)
                    {
                        f |= submask;
                    }
                    else
                    {
                        f &= (byte)~submask;
                    }
                    break;
            }
        }
        private ushort GetReg(Registers reg)
        {
            switch (reg)
            {
                case Registers.A: return a;
                case Registers.B: return b;
                case Registers.C: return c;
                case Registers.D: return d;
                case Registers.E: return e;
                case Registers.F: return f;
                case Registers.H: return h;
                case Registers.L: return l;

                case Registers.AF: return af;
                case Registers.BC: return bc;
                case Registers.DE: return de;
                case Registers.HL: return hl;

                case Registers.SP: return sp;
                case Registers.PC: return pc;

                //Immidiates for instructions
                case Registers.n: return Imm8bitvalue();
                case Registers.nn: return Imm16bitvalue();

                default: return 0;
            }
        }
        private void SetReg(Registers reg, ushort value)
        {
            switch (reg)
            {
                case Registers.A: a = (byte)value; break;
                case Registers.B: b = (byte)value; break;
                case Registers.C: c = (byte)value; break;
                case Registers.D: d = (byte)value; break;
                case Registers.E: e = (byte)value; break;
                case Registers.F: f = (byte)value; break;
                case Registers.H: h = (byte)value; break;
                case Registers.L: l = (byte)value; break;

                case Registers.AF: af = value; break;
                case Registers.BC: bc = value; break;
                case Registers.DE: de = value; break;
                case Registers.HL: hl = value; break;

                case Registers.SP: sp = value; break;
                case Registers.PC: pc = value; break;
                default: break;
            }
        }


        private void ExecuteInstruction()
        {
            opcode = Imm8bitvalue();
            switch (opcode)
            {
                //NOP
                case 0x00: NOP(); break;

                //Extended OPCODE-table
                case 0xCB: ExtOpcode(); break;

                //LD nn,n
                case 0x06: LD_nn_n(Registers.B); break;
                case 0x0E: LD_nn_n(Registers.C); break;
                case 0x16: LD_nn_n(Registers.D); break;
                case 0x1E: LD_nn_n(Registers.E); break;
                case 0x26: LD_nn_n(Registers.H); break;
                case 0x2E: LD_nn_n(Registers.L); break;

                //XOR n
                case 0xAF: Xor_n(Registers.A); break;
                case 0xA8: Xor_n(Registers.B); break;
                case 0xA9: Xor_n(Registers.C); break;
                case 0xAA: Xor_n(Registers.D); break;
                case 0xAB: Xor_n(Registers.E); break;
                case 0xAC: Xor_n(Registers.H); break;
                case 0xAD: Xor_n(Registers.L); break;
                case 0xAE: Xor_n(Registers.HL); break;
                case 0xEE: Xor_n(Registers.n); break;

                //LD_r1_r1
                //A
                case 0x7F: LD_r1_r2(Registers.A, Registers.A); break;
                case 0x78: LD_r1_r2(Registers.A, Registers.B); break;
                case 0x79: LD_r1_r2(Registers.A, Registers.C); break;
                case 0x7A: LD_r1_r2(Registers.A, Registers.D); break;
                case 0x7B: LD_r1_r2(Registers.A, Registers.E); break;
                case 0x7C: LD_r1_r2(Registers.A, Registers.H); break;
                case 0x7D: LD_r1_r2(Registers.A, Registers.L); break;
                case 0x7E: LD_r1_r2(Registers.A, Registers.HL); break;
                //B
                case 0x40: LD_r1_r2(Registers.B, Registers.B); break;
                case 0x41: LD_r1_r2(Registers.B, Registers.C); break;
                case 0x42: LD_r1_r2(Registers.B, Registers.D); break;
                case 0x43: LD_r1_r2(Registers.B, Registers.E); break;
                case 0x44: LD_r1_r2(Registers.B, Registers.H); break;
                case 0x45: LD_r1_r2(Registers.B, Registers.L); break;
                case 0x46: LD_r1_r2(Registers.B, Registers.HL); break;
                //C
                case 0x48: LD_r1_r2(Registers.C, Registers.B); break;
                case 0x49: LD_r1_r2(Registers.C, Registers.C); break;
                case 0x4A: LD_r1_r2(Registers.C, Registers.D); break;
                case 0x4B: LD_r1_r2(Registers.C, Registers.E); break;
                case 0x4C: LD_r1_r2(Registers.C, Registers.H); break;
                case 0x4D: LD_r1_r2(Registers.C, Registers.L); break;
                case 0x4E: LD_r1_r2(Registers.C, Registers.HL); break;
                //D
                case 0x50: LD_r1_r2(Registers.D, Registers.B); break;
                case 0x51: LD_r1_r2(Registers.D, Registers.C); break;
                case 0x52: LD_r1_r2(Registers.D, Registers.D); break;
                case 0x53: LD_r1_r2(Registers.D, Registers.E); break;
                case 0x54: LD_r1_r2(Registers.D, Registers.H); break;
                case 0x55: LD_r1_r2(Registers.D, Registers.L); break;
                case 0x56: LD_r1_r2(Registers.D, Registers.HL); break;
                //E
                case 0x58: LD_r1_r2(Registers.E, Registers.B); break;
                case 0x59: LD_r1_r2(Registers.E, Registers.C); break;
                case 0x5A: LD_r1_r2(Registers.E, Registers.D); break;
                case 0x5B: LD_r1_r2(Registers.E, Registers.E); break;
                case 0x5C: LD_r1_r2(Registers.E, Registers.H); break;
                case 0x5D: LD_r1_r2(Registers.E, Registers.L); break;
                case 0x5E: LD_r1_r2(Registers.E, Registers.HL); break;
                //H
                case 0x60: LD_r1_r2(Registers.H, Registers.B); break;
                case 0x61: LD_r1_r2(Registers.H, Registers.C); break;
                case 0x62: LD_r1_r2(Registers.H, Registers.D); break;
                case 0x63: LD_r1_r2(Registers.H, Registers.E); break;
                case 0x64: LD_r1_r2(Registers.H, Registers.H); break;
                case 0x65: LD_r1_r2(Registers.H, Registers.L); break;
                case 0x66: LD_r1_r2(Registers.H, Registers.HL); break;
                //L
                case 0x68: LD_r1_r2(Registers.L, Registers.B); break;
                case 0x69: LD_r1_r2(Registers.L, Registers.C); break;
                case 0x6A: LD_r1_r2(Registers.L, Registers.D); break;
                case 0x6B: LD_r1_r2(Registers.L, Registers.E); break;
                case 0x6C: LD_r1_r2(Registers.L, Registers.H); break;
                case 0x6D: LD_r1_r2(Registers.L, Registers.L); break;
                case 0x6E: LD_r1_r2(Registers.L, Registers.HL); break;

                //HL
                case 0x70: LD_r1_r2(Registers.HL, Registers.B); break;
                case 0x71: LD_r1_r2(Registers.HL, Registers.C); break;
                case 0x72: LD_r1_r2(Registers.HL, Registers.D); break;
                case 0x73: LD_r1_r2(Registers.HL, Registers.E); break;
                case 0x74: LD_r1_r2(Registers.HL, Registers.H); break;
                case 0x75: LD_r1_r2(Registers.HL, Registers.L); break;
                case 0x36: LD_r1_r2(Registers.HL, Registers.n); break;

                //LD_a_n
                case 0x0A: LD_a_n(Registers.BC); break;
                case 0x1A: LD_a_n(Registers.DE); break;
                case 0xFA: LD_a_n(Registers.nn); break;
                case 0x3E: LD_a_n(Registers.n); break;

                //LD_n_a
                case 0x47: LD_n_a(Registers.B); break;
                case 0x4F: LD_n_a(Registers.C); break;
                case 0x57: LD_n_a(Registers.D); break;
                case 0x5F: LD_n_a(Registers.E); break;
                case 0x67: LD_n_a(Registers.H); break;
                case 0x6F: LD_n_a(Registers.L); break;
                case 0x02: LD_n_a(Registers.BC); break;
                case 0x12: LD_n_a(Registers.DE); break;
                case 0x77: LD_n_a(Registers.HL); break;
                case 0xEA: LD_n_a(Registers.nn); break;

                //LD_a_c
                case 0xF2: LD_a_c(); break;

                //LD_c_a
                case 0xE2: LD_c_a(); break;

                //LD_a_hldec
                case 0x3A: LD_a_hldec(); break;
                //LD_hldec_a
                case 0x32: LD_hldec_a(); break;
                //LD_a_hlinc
                case 0x2A: LD_a_hlinc(); break;
                //LD_hlinc_a
                case 0x22: LD_hlinc_a(); break;
                //LDH_n_a
                case 0xE0: LDH_n_a(); break;
                //LDH_a_n
                case 0xF0: LDH_a_n(); break;

                //16Bit loads
                //LD_n_nn
                case 0x01: LD_n_nn(Registers.BC); break;
                case 0x11: LD_n_nn(Registers.DE); break;
                case 0x21: LD_n_nn(Registers.HL); break;
                case 0x31: LD_n_nn(Registers.SP); break;
                //LD_sp_hl
                case 0xF9: LD_sp_hl(); break;

                //LD_hl_sp_n 
                case 0xF8: LD_hl_sp_n(); break;

                //LD_nn_SP
                case 0x08: LD_nn_sp(); break;

                //PUSH_nn
                case 0xF5: Push_nn(Registers.AF); break;
                case 0xC5: Push_nn(Registers.BC); break;
                case 0xD5: Push_nn(Registers.DE); break;
                case 0xE5: Push_nn(Registers.HL); break;

                //POP_nn
                case 0xF1: Pop_nn(Registers.AF); break;
                case 0xC1: Pop_nn(Registers.BC); break;
                case 0xD1: Pop_nn(Registers.DE); break;
                case 0xE1: Pop_nn(Registers.HL); break;

                //add_a_n
                case 0x87: Add_a_n(Registers.A); break;
                case 0x80: Add_a_n(Registers.B); break;
                case 0x81: Add_a_n(Registers.C); break;
                case 0x82: Add_a_n(Registers.D); break;
                case 0x83: Add_a_n(Registers.E); break;
                case 0x84: Add_a_n(Registers.H); break;
                case 0x85: Add_a_n(Registers.L); break;
                case 0x86: Add_a_n(Registers.HL); break;
                case 0xC6: Add_a_n(Registers.n); break;

                //Adc_a_n
                case 0x8F: Adc_a_n(Registers.A); break;
                case 0x88: Adc_a_n(Registers.B); break;
                case 0x89: Adc_a_n(Registers.C); break;
                case 0x8A: Adc_a_n(Registers.D); break;
                case 0x8B: Adc_a_n(Registers.E); break;
                case 0x8C: Adc_a_n(Registers.H); break;
                case 0x8D: Adc_a_n(Registers.L); break;
                case 0x8E: Adc_a_n(Registers.HL); break;
                case 0xCE: Adc_a_n(Registers.n); break;

                //Sub_a_n

                case 0x97: Sub_a_n(Registers.A); break;
                case 0x90: Sub_a_n(Registers.B); break;
                case 0x91: Sub_a_n(Registers.C); break;
                case 0x92: Sub_a_n(Registers.D); break;
                case 0x93: Sub_a_n(Registers.E); break;
                case 0x94: Sub_a_n(Registers.H); break;
                case 0x95: Sub_a_n(Registers.L); break;
                case 0x96: Sub_a_n(Registers.HL); break;
                case 0xD6: Sub_a_n(Registers.n); break;

                //Scb_a_n

                case 0x9F: Sbc_a_n(Registers.A); break;
                case 0x98: Sbc_a_n(Registers.B); break;
                case 0x99: Sbc_a_n(Registers.C); break;
                case 0x9A: Sbc_a_n(Registers.D); break;
                case 0x9B: Sbc_a_n(Registers.E); break;
                case 0x9C: Sbc_a_n(Registers.H); break;
                case 0x9D: Sbc_a_n(Registers.L); break;
                case 0x9E: Sbc_a_n(Registers.HL); break;
                case 0xDE: Sbc_a_n(Registers.n); break;

                //And_n
                case 0xA7: And_n(Registers.A); break;
                case 0xA0: And_n(Registers.B); break;
                case 0xA1: And_n(Registers.C); break;
                case 0xA2: And_n(Registers.D); break;
                case 0xA3: And_n(Registers.E); break;
                case 0xA4: And_n(Registers.H); break;
                case 0xA5: And_n(Registers.L); break;
                case 0xA6: And_n(Registers.HL); break;
                case 0xE6: And_n(Registers.n); break;

                //Or_n
                case 0xB7: Or_n(Registers.A); break;
                case 0xB0: Or_n(Registers.B); break;
                case 0xB1: Or_n(Registers.C); break;
                case 0xB2: Or_n(Registers.D); break;
                case 0xB3: Or_n(Registers.E); break;
                case 0xB4: Or_n(Registers.H); break;
                case 0xB5: Or_n(Registers.L); break;
                case 0xB6: Or_n(Registers.HL); break;
                case 0xF6: Or_n(Registers.n); break;

                //CP_n
                case 0xBF: Cp_n(Registers.A); break;
                case 0xB8: Cp_n(Registers.B); break;
                case 0xB9: Cp_n(Registers.C); break;
                case 0xBA: Cp_n(Registers.D); break;
                case 0xBB: Cp_n(Registers.E); break;
                case 0xBC: Cp_n(Registers.H); break;
                case 0xBD: Cp_n(Registers.L); break;
                case 0xBE: Cp_n(Registers.HL); break;
                case 0xFE: Cp_n(Registers.n); break;

                //Inc_n
                case 0x3C: Inc_n(Registers.A); break;
                case 0x04: Inc_n(Registers.B); break;
                case 0x0C: Inc_n(Registers.C); break;
                case 0x14: Inc_n(Registers.D); break;
                case 0x1C: Inc_n(Registers.E); break;
                case 0x24: Inc_n(Registers.H); break;
                case 0x2C: Inc_n(Registers.L); break;
                case 0x34: Inc_n(Registers.HL); break;

                //Dec_n
                case 0x3D: Dec_n(Registers.A); break;
                case 0x05: Dec_n(Registers.B); break;
                case 0x0D: Dec_n(Registers.C); break;
                case 0x15: Dec_n(Registers.D); break;
                case 0x1D: Dec_n(Registers.E); break;
                case 0x25: Dec_n(Registers.H); break;
                case 0x2D: Dec_n(Registers.L); break;
                case 0x35: Dec_n(Registers.HL); break;

                //Add_hl_n
                case 0x09: Add_hl_n(Registers.BC); break;
                case 0x19: Add_hl_n(Registers.DE); break;
                case 0x29: Add_hl_n(Registers.HL); break;
                case 0x39: Add_hl_n(Registers.SP); break;

                //Add_sp_n
                case 0xE8: Add_sp_n(Registers.n); break;

                //Inc_nn
                case 0x03: Inc_nn(Registers.BC); break;
                case 0x13: Inc_nn(Registers.DE); break;
                case 0x23: Inc_nn(Registers.HL); break;
                case 0x33: Inc_nn(Registers.SP); break;

                //Dec_nn
                case 0x0B: Dec_nn(Registers.BC); break;
                case 0x1B: Dec_nn(Registers.DE); break;
                case 0x2B: Dec_nn(Registers.HL); break;
                case 0x3B: Dec_nn(Registers.SP); break;


                //Decimal adjust pain in the ass, finally works!
                case 0x27: Daa(); break;

                //Complement A reg CPL
                case 0x2F:
                    cycles += 4;
                    SetReg(Registers.A, (ushort)~GetReg(Registers.A));
                    SetFlag(Flags.SubFlag, 1);
                    SetFlag(Flags.HalfCarry, 1);
                    break;
                //Complement Carryflag CCF
                case 0x3F:
                    cycles += 4;
                    SetFlag(Flags.Carry, (byte)(GetFlag(Flags.Carry) == 0 ? 1 : 0));
                    SetFlag(Flags.HalfCarry, 0);
                    SetFlag(Flags.SubFlag, 0);
                    break;
                //Set carry flag SCF
                case 0x37:
                    cycles += 4;
                    SetFlag(Flags.Carry, 1);
                    SetFlag(Flags.HalfCarry, 0);
                    SetFlag(Flags.SubFlag, 0);
                    break;
                //Halt
                case 0x76:
                    cycles += 4;
                    halt = true;
                    break;
                //Stop
                case 0x10:
                    cycles += 4;
                    var temp = Imm8bitvalue();
                    stop = true;
                    break;
                //Interupt enable and disable, should happen after next instruction
                //Disable
                case 0xF3:
                    cycles += 4;
                    interuptsenabled = false;
                    break;
                //Enable
                case 0xFB:
                    cycles += 4;
                    interuptsenabled = true;
                    break;
                //RLCA
                case 0x07: RLCA(); break;
                //RLA
                case 0x17: RLA(); break;
                //RRCA
                case 0x0F: RRCA(); break;
                //RRA
                case 0x1F: RRA(); break;



                //Jump
                case 0xC3: Jp_nn(); break;
                // Conditional Jumps
                case 0xC2: if (GetFlag(Flags.ZeroFlag) == 0) { Jp_nn(); } else { var getridof = Imm16bitvalue(); } break;
                case 0xCA: if (GetFlag(Flags.ZeroFlag) == 1) { Jp_nn(); } else { var getridof = Imm16bitvalue(); } break;
                case 0xD2: if (GetFlag(Flags.Carry) == 0) { Jp_nn(); } else { var getridof = Imm16bitvalue(); } break;
                case 0xDA: if (GetFlag(Flags.Carry) == 1) { Jp_nn(); } else { var getridof = Imm16bitvalue(); } break;
                //Jp_hl
                case 0xE9: Jp_hl(); break;
                //Jr_n
                case 0x18: Jr_n(); break;
                // Conditional Jumps add immidiate value
                case 0x20: if (GetFlag(Flags.ZeroFlag) == 0) { Jr_n(); } else { var getridof = Imm8bitvalue(); } break;
                case 0x28: if (GetFlag(Flags.ZeroFlag) == 1) { Jr_n(); } else { var getridof = Imm8bitvalue(); } break;
                case 0x30: if (GetFlag(Flags.Carry) == 0) { Jr_n(); } else { var getridof = Imm8bitvalue(); } break;
                case 0x38: if (GetFlag(Flags.Carry) == 1) { Jr_n(); } else { var getridof = Imm8bitvalue(); } break;

                //Call
                case 0xCD: Call(); break;
                //Conditional calls
                case 0xC4: if (GetFlag(Flags.ZeroFlag) == 0) { Call(); } else { var getridof = Imm16bitvalue(); } break;
                case 0xCC: if (GetFlag(Flags.ZeroFlag) == 1) { Call(); } else { var getridof = Imm16bitvalue(); } break;
                case 0xD4: if (GetFlag(Flags.Carry) == 0) { Call(); } else { var getridof = Imm16bitvalue(); } break;
                case 0xDC: if (GetFlag(Flags.Carry) == 1) { Call(); } else { var getridof = Imm16bitvalue(); } break;
                //Restart
                case 0xC7: Rst_n(0x00); break;
                case 0xCF: Rst_n(0x08); break;
                case 0xD7: Rst_n(0x10); break;
                case 0xDF: Rst_n(0x18); break;
                case 0xE7: Rst_n(0x20); break;
                case 0xEF: Rst_n(0x28); break;
                case 0xF7: Rst_n(0x30); break;
                case 0xFF: Rst_n(0x38); break;
                //Ret
                case 0xC9: Ret(); break;
                //Conditional Returns
                case 0xC0: if (GetFlag(Flags.ZeroFlag) == 0) { Ret(); } break;
                case 0xC8: if (GetFlag(Flags.ZeroFlag) == 1) { Ret(); } break;
                case 0xD0: if (GetFlag(Flags.Carry) == 0) { Ret(); } break;
                case 0xD8: if (GetFlag(Flags.Carry) == 1) { Ret(); } break;
                //Reti
                case 0xD9: Reti(); break;


                default: /* Console.WriteLine("Opcode: {0:X} Not yet implemented", opcode); */ break;
            }
        }
        private void ExtOpcode()
        {
            opcode = Imm8bitvalue();
            switch (opcode)
            {
                //Swap_n
                case 0x37: Swap_n(Registers.A); break;
                case 0x30: Swap_n(Registers.B); break;
                case 0x31: Swap_n(Registers.C); break;
                case 0x32: Swap_n(Registers.D); break;
                case 0x33: Swap_n(Registers.E); break;
                case 0x34: Swap_n(Registers.H); break;
                case 0x35: Swap_n(Registers.L); break;
                case 0x36: Swap_n(Registers.HL); break;
                //Rlc_n  
                case 0x07: Rlc_n(Registers.A); break;
                case 0x00: Rlc_n(Registers.B); break;
                case 0x01: Rlc_n(Registers.C); break;
                case 0x02: Rlc_n(Registers.D); break;
                case 0x03: Rlc_n(Registers.E); break;
                case 0x04: Rlc_n(Registers.H); break;
                case 0x05: Rlc_n(Registers.L); break;
                case 0x06: Rlc_n(Registers.HL); break;
                //Rl_n
                case 0x17: Rl_n(Registers.A); break;
                case 0x10: Rl_n(Registers.B); break;
                case 0x11: Rl_n(Registers.C); break;
                case 0x12: Rl_n(Registers.D); break;
                case 0x13: Rl_n(Registers.E); break;
                case 0x14: Rl_n(Registers.H); break;
                case 0x15: Rl_n(Registers.L); break;
                case 0x16: Rl_n(Registers.HL); break;
                //Rrc_n
                case 0x0F: Rrc_n(Registers.A); break;
                case 0x08: Rrc_n(Registers.B); break;
                case 0x09: Rrc_n(Registers.C); break;
                case 0x0A: Rrc_n(Registers.D); break;
                case 0x0B: Rrc_n(Registers.E); break;
                case 0x0C: Rrc_n(Registers.H); break;
                case 0x0D: Rrc_n(Registers.L); break;
                case 0x0E: Rrc_n(Registers.HL); break;
                //Rr_n
                case 0x1F: Rr_n(Registers.A); break;
                case 0x18: Rr_n(Registers.B); break;
                case 0x19: Rr_n(Registers.C); break;
                case 0x1A: Rr_n(Registers.D); break;
                case 0x1B: Rr_n(Registers.E); break;
                case 0x1C: Rr_n(Registers.H); break;
                case 0x1D: Rr_n(Registers.L); break;
                case 0x1E: Rr_n(Registers.HL); break;
                //Sla_n
                case 0x27: Sla_n(Registers.A); break;
                case 0x20: Sla_n(Registers.B); break;
                case 0x21: Sla_n(Registers.C); break;
                case 0x22: Sla_n(Registers.D); break;
                case 0x23: Sla_n(Registers.E); break;
                case 0x24: Sla_n(Registers.H); break;
                case 0x25: Sla_n(Registers.L); break;
                case 0x26: Sla_n(Registers.HL); break;
                //Sra_n
                case 0x2F: Sra_n(Registers.A); break;
                case 0x28: Sra_n(Registers.B); break;
                case 0x29: Sra_n(Registers.C); break;
                case 0x2A: Sra_n(Registers.D); break;
                case 0x2B: Sra_n(Registers.E); break;
                case 0x2C: Sra_n(Registers.H); break;
                case 0x2D: Sra_n(Registers.L); break;
                case 0x2E: Sra_n(Registers.HL); break;
                //Sln_n
                case 0x3F: Srl_n(Registers.A); break;
                case 0x38: Srl_n(Registers.B); break;
                case 0x39: Srl_n(Registers.C); break;
                case 0x3A: Srl_n(Registers.D); break;
                case 0x3B: Srl_n(Registers.E); break;
                case 0x3C: Srl_n(Registers.H); break;
                case 0x3D: Srl_n(Registers.L); break;
                case 0x3E: Srl_n(Registers.HL); break;
                //Bit
                case 0x40: Bit_n(0, Registers.B); break;
                case 0x41: Bit_n(0, Registers.C); break;
                case 0x42: Bit_n(0, Registers.D); break;
                case 0x43: Bit_n(0, Registers.E); break;
                case 0x44: Bit_n(0, Registers.H); break;
                case 0x45: Bit_n(0, Registers.L); break;
                case 0x46: Bit_n(0, Registers.HL); break;
                case 0x47: Bit_n(0, Registers.A); break;

                case 0x48: Bit_n(1, Registers.B); break;
                case 0x49: Bit_n(1, Registers.C); break;
                case 0x4A: Bit_n(1, Registers.D); break;
                case 0x4B: Bit_n(1, Registers.E); break;
                case 0x4C: Bit_n(1, Registers.H); break;
                case 0x4D: Bit_n(1, Registers.L); break;
                case 0x4E: Bit_n(1, Registers.HL); break;
                case 0x4F: Bit_n(1, Registers.A); break;

                case 0x50: Bit_n(2, Registers.B); break;
                case 0x51: Bit_n(2, Registers.C); break;
                case 0x52: Bit_n(2, Registers.D); break;
                case 0x53: Bit_n(2, Registers.E); break;
                case 0x54: Bit_n(2, Registers.H); break;
                case 0x55: Bit_n(2, Registers.L); break;
                case 0x56: Bit_n(2, Registers.HL); break;
                case 0x57: Bit_n(2, Registers.A); break;

                case 0x58: Bit_n(3, Registers.B); break;
                case 0x59: Bit_n(3, Registers.C); break;
                case 0x5A: Bit_n(3, Registers.D); break;
                case 0x5B: Bit_n(3, Registers.E); break;
                case 0x5C: Bit_n(3, Registers.H); break;
                case 0x5D: Bit_n(3, Registers.L); break;
                case 0x5E: Bit_n(3, Registers.HL); break;
                case 0x5F: Bit_n(3, Registers.A); break;

                case 0x60: Bit_n(4, Registers.B); break;
                case 0x61: Bit_n(4, Registers.C); break;
                case 0x62: Bit_n(4, Registers.D); break;
                case 0x63: Bit_n(4, Registers.E); break;
                case 0x64: Bit_n(4, Registers.H); break;
                case 0x65: Bit_n(4, Registers.L); break;
                case 0x66: Bit_n(4, Registers.HL); break;
                case 0x67: Bit_n(4, Registers.A); break;

                case 0x68: Bit_n(5, Registers.B); break;
                case 0x69: Bit_n(5, Registers.C); break;
                case 0x6A: Bit_n(5, Registers.D); break;
                case 0x6B: Bit_n(5, Registers.E); break;
                case 0x6C: Bit_n(5, Registers.H); break;
                case 0x6D: Bit_n(5, Registers.L); break;
                case 0x6E: Bit_n(5, Registers.HL); break;
                case 0x6F: Bit_n(5, Registers.A); break;

                case 0x70: Bit_n(6, Registers.B); break;
                case 0x71: Bit_n(6, Registers.C); break;
                case 0x72: Bit_n(6, Registers.D); break;
                case 0x73: Bit_n(6, Registers.E); break;
                case 0x74: Bit_n(6, Registers.H); break;
                case 0x75: Bit_n(6, Registers.L); break;
                case 0x76: Bit_n(6, Registers.HL); break;
                case 0x77: Bit_n(6, Registers.A); break;

                case 0x78: Bit_n(7, Registers.B); break;
                case 0x79: Bit_n(7, Registers.C); break;
                case 0x7A: Bit_n(7, Registers.D); break;
                case 0x7B: Bit_n(7, Registers.E); break;
                case 0x7C: Bit_n(7, Registers.H); break;
                case 0x7D: Bit_n(7, Registers.L); break;
                case 0x7E: Bit_n(7, Registers.HL); break;
                case 0x7F: Bit_n(7, Registers.A); break;
                //Res
                case 0x80: Res_n(0, Registers.B); break;
                case 0x81: Res_n(0, Registers.C); break;
                case 0x82: Res_n(0, Registers.D); break;
                case 0x83: Res_n(0, Registers.E); break;
                case 0x84: Res_n(0, Registers.H); break;
                case 0x85: Res_n(0, Registers.L); break;
                case 0x86: Res_n(0, Registers.HL); break;
                case 0x87: Res_n(0, Registers.A); break;

                case 0x88: Res_n(1, Registers.B); break;
                case 0x89: Res_n(1, Registers.C); break;
                case 0x8A: Res_n(1, Registers.D); break;
                case 0x8B: Res_n(1, Registers.E); break;
                case 0x8C: Res_n(1, Registers.H); break;
                case 0x8D: Res_n(1, Registers.L); break;
                case 0x8E: Res_n(1, Registers.HL); break;
                case 0x8F: Res_n(1, Registers.A); break;

                case 0x90: Res_n(2, Registers.B); break;
                case 0x91: Res_n(2, Registers.C); break;
                case 0x92: Res_n(2, Registers.D); break;
                case 0x93: Res_n(2, Registers.E); break;
                case 0x94: Res_n(2, Registers.H); break;
                case 0x95: Res_n(2, Registers.L); break;
                case 0x96: Res_n(2, Registers.HL); break;
                case 0x97: Res_n(2, Registers.A); break;

                case 0x98: Res_n(3, Registers.B); break;
                case 0x99: Res_n(3, Registers.C); break;
                case 0x9A: Res_n(3, Registers.D); break;
                case 0x9B: Res_n(3, Registers.E); break;
                case 0x9C: Res_n(3, Registers.H); break;
                case 0x9D: Res_n(3, Registers.L); break;
                case 0x9E: Res_n(3, Registers.HL); break;
                case 0x9F: Res_n(3, Registers.A); break;

                case 0xA0: Res_n(4, Registers.B); break;
                case 0xA1: Res_n(4, Registers.C); break;
                case 0xA2: Res_n(4, Registers.D); break;
                case 0xA3: Res_n(4, Registers.E); break;
                case 0xA4: Res_n(4, Registers.H); break;
                case 0xA5: Res_n(4, Registers.L); break;
                case 0xA6: Res_n(4, Registers.HL); break;
                case 0xA7: Res_n(4, Registers.A); break;

                case 0xA8: Res_n(5, Registers.B); break;
                case 0xA9: Res_n(5, Registers.C); break;
                case 0xAA: Res_n(5, Registers.D); break;
                case 0xAB: Res_n(5, Registers.E); break;
                case 0xAC: Res_n(5, Registers.H); break;
                case 0xAD: Res_n(5, Registers.L); break;
                case 0xAE: Res_n(5, Registers.HL); break;
                case 0xAF: Res_n(5, Registers.A); break;

                case 0xB0: Res_n(6, Registers.B); break;
                case 0xB1: Res_n(6, Registers.C); break;
                case 0xB2: Res_n(6, Registers.D); break;
                case 0xB3: Res_n(6, Registers.E); break;
                case 0xB4: Res_n(6, Registers.H); break;
                case 0xB5: Res_n(6, Registers.L); break;
                case 0xB6: Res_n(6, Registers.HL); break;
                case 0xB7: Res_n(6, Registers.A); break;

                case 0xB8: Res_n(7, Registers.B); break;
                case 0xB9: Res_n(7, Registers.C); break;
                case 0xBA: Res_n(7, Registers.D); break;
                case 0xBB: Res_n(7, Registers.E); break;
                case 0xBC: Res_n(7, Registers.H); break;
                case 0xBD: Res_n(7, Registers.L); break;
                case 0xBE: Res_n(7, Registers.HL); break;
                case 0xBF: Res_n(7, Registers.A); break;
                //Set
                case 0xC0: Set_n(0, Registers.B); break;
                case 0xC1: Set_n(0, Registers.C); break;
                case 0xC2: Set_n(0, Registers.D); break;
                case 0xC3: Set_n(0, Registers.E); break;
                case 0xC4: Set_n(0, Registers.H); break;
                case 0xC5: Set_n(0, Registers.L); break;
                case 0xC6: Set_n(0, Registers.HL); break;
                case 0xC7: Set_n(0, Registers.A); break;

                case 0xC8: Set_n(1, Registers.B); break;
                case 0xC9: Set_n(1, Registers.C); break;
                case 0xCA: Set_n(1, Registers.D); break;
                case 0xCB: Set_n(1, Registers.E); break;
                case 0xCC: Set_n(1, Registers.H); break;
                case 0xCD: Set_n(1, Registers.L); break;
                case 0xCE: Set_n(1, Registers.HL); break;
                case 0xCF: Set_n(1, Registers.A); break;

                case 0xD0: Set_n(2, Registers.B); break;
                case 0xD1: Set_n(2, Registers.C); break;
                case 0xD2: Set_n(2, Registers.D); break;
                case 0xD3: Set_n(2, Registers.E); break;
                case 0xD4: Set_n(2, Registers.H); break;
                case 0xD5: Set_n(2, Registers.L); break;
                case 0xD6: Set_n(2, Registers.HL); break;
                case 0xD7: Set_n(2, Registers.A); break;

                case 0xD8: Set_n(3, Registers.B); break;
                case 0xD9: Set_n(3, Registers.C); break;
                case 0xDA: Set_n(3, Registers.D); break;
                case 0xDB: Set_n(3, Registers.E); break;
                case 0xDC: Set_n(3, Registers.H); break;
                case 0xDD: Set_n(3, Registers.L); break;
                case 0xDE: Set_n(3, Registers.HL); break;
                case 0xDF: Set_n(3, Registers.A); break;

                case 0xE0: Set_n(4, Registers.B); break;
                case 0xE1: Set_n(4, Registers.C); break;
                case 0xE2: Set_n(4, Registers.D); break;
                case 0xE3: Set_n(4, Registers.E); break;
                case 0xE4: Set_n(4, Registers.H); break;
                case 0xE5: Set_n(4, Registers.L); break;
                case 0xE6: Set_n(4, Registers.HL); break;
                case 0xE7: Set_n(4, Registers.A); break;

                case 0xE8: Set_n(5, Registers.B); break;
                case 0xE9: Set_n(5, Registers.C); break;
                case 0xEA: Set_n(5, Registers.D); break;
                case 0xEB: Set_n(5, Registers.E); break;
                case 0xEC: Set_n(5, Registers.H); break;
                case 0xED: Set_n(5, Registers.L); break;
                case 0xEE: Set_n(5, Registers.HL); break;
                case 0xEF: Set_n(5, Registers.A); break;

                case 0xF0: Set_n(6, Registers.B); break;
                case 0xF1: Set_n(6, Registers.C); break;
                case 0xF2: Set_n(6, Registers.D); break;
                case 0xF3: Set_n(6, Registers.E); break;
                case 0xF4: Set_n(6, Registers.H); break;
                case 0xF5: Set_n(6, Registers.L); break;
                case 0xF6: Set_n(6, Registers.HL); break;
                case 0xF7: Set_n(6, Registers.A); break;

                case 0xF8: Set_n(7, Registers.B); break;
                case 0xF9: Set_n(7, Registers.C); break;
                case 0xFA: Set_n(7, Registers.D); break;
                case 0xFB: Set_n(7, Registers.E); break;
                case 0xFC: Set_n(7, Registers.H); break;
                case 0xFD: Set_n(7, Registers.L); break;
                case 0xFE: Set_n(7, Registers.HL); break;
                case 0xFF: Set_n(7, Registers.A); break;


                default: /* Console.WriteLine("Extended Opcode: {0:X} Not yet implemented", opcode); */ break;
            }

        }
    

        #region Instructions
        private void NOP()
        {
            cycles += 4;
        }
        private void Call()
        {
            cycles += 12;
            SetReg(Registers.SP, (ushort)(GetReg(Registers.SP) - 1));
            _core.WriteMem(GetReg(Registers.SP), (byte)((GetReg(Registers.PC) + 2) >> 8));
            SetReg(Registers.SP, (ushort)(GetReg(Registers.SP) - 1));
            _core.WriteMem(GetReg(Registers.SP), (byte)((GetReg(Registers.PC) + 2) & 0xFF));
            SetReg(Registers.PC, Imm16bitvalue());
        }
        private void Jp_nn() // Jump to 16bit immidiate value
        {
            cycles += 12;
            SetReg(Registers.PC, Imm16bitvalue());

        }
        private void Jp_hl() //Jump to value in HL
        {
            cycles += 4;
            SetReg(Registers.PC, GetReg(Registers.HL));
        }
        private void Jr_n() // Add SIGNED immidiate value to current address and jump
        {
            cycles += 8;
            sbyte immvalue = (sbyte)GetReg(Registers.n);
            SetReg(Registers.PC, (ushort)(GetReg(Registers.PC) + immvalue));
        }
        private void LD_nn_n(Registers reg) //Load next 8bit value into reg
        {
            cycles += 8;
            SetReg(reg, Imm8bitvalue());
        }
        private void Daa() //Decimal adjust A IT FINALLY WORKS
        {
            cycles += 4;
            byte temp = (byte)GetReg(Registers.A);
            if (GetFlag(Flags.SubFlag) == 0)
            {
                if (temp > 0x99 || GetFlag(Flags.Carry) != 0)
                {
                    temp += 0x60;
                    SetFlag(Flags.Carry, 1);
                }
                if ((temp & 0x0F) > 0x09 || GetFlag(Flags.HalfCarry) != 0)
                {
                    temp += 0x06;
                }
            }
            else 
            {
                if (GetFlag(Flags.Carry) != 0)
                    temp -= 0x60;
                if (GetFlag(Flags.HalfCarry) != 0)
                    temp -= 0x06;
            }
            SetFlag(Flags.HalfCarry, 0);
            SetFlag(Flags.ZeroFlag, (byte)(temp != 0 ? 0 : 1));

            SetReg(Registers.A, temp);

        }
        private void LD_r1_r2(Registers reg1, Registers reg2) //load value in reg2 into reg1
        {
            if (reg1 == Registers.HL)
            {
                if (reg2 == Registers.n)
                {
                    _core.WriteMem(GetReg(Registers.HL), (byte)GetReg(Registers.n));
                    cycles += 12;
                }
                else
                {
                    _core.WriteMem(GetReg(Registers.HL), (byte)GetReg(reg2));
                    cycles += 8;
                }
            }
            else
            {
                if (reg2 == Registers.HL)
                {
                    SetReg(reg1, _core.ReadMem(GetReg(Registers.HL)));
                    cycles += 8;
                }
                else
                {
                    SetReg(reg1, GetReg(reg2));
                    cycles += 4;
                }
            }

        }
        private void LD_a_n(Registers reg) //Load reg into A
        {
            if (reg == Registers.BC || reg == Registers.DE || reg == Registers.HL)
            {
                cycles += 8;
                SetReg(Registers.A, _core.ReadMem(GetReg(reg)));
            }
            else if (reg == Registers.nn)
            {
                cycles += 16;
                SetReg(Registers.A, _core.ReadMem(GetReg(reg)));

            }
            else if (reg == Registers.n)
            {
                cycles += 8;
                SetReg(Registers.A, GetReg(reg));
            }
            else
            {
                cycles += 4;
                SetReg(Registers.A, GetReg(reg));
            }
        }
        private void LD_n_a(Registers reg) //Put value A into n.
        {
            if (reg == Registers.BC || reg == Registers.DE || reg == Registers.HL)
            {
                cycles += 8;
                _core.WriteMem(GetReg(reg), (byte)GetReg(Registers.A));
            }
            else if (reg == Registers.nn)
            {
                cycles += 16;
                _core.WriteMem(GetReg(reg), (byte)GetReg(Registers.A));
            }
            else
            {
                cycles += 4;
                SetReg(reg, (byte)GetReg(Registers.A));
            }
        }
        private void LD_a_c() // Put value at adress ff00 + regc into a
        {
            cycles += 8;
            SetReg(Registers.A, _core.ReadMem((ushort)(GetReg(Registers.C) + 0xFF00)));
        }
        private void LD_c_a() // Put A at adress ff00 + regc
        {
            cycles += 8;
            _core.WriteMem((ushort)(GetReg(Registers.C) + 0xFF00), (byte)GetReg(Registers.A));
        }
        private void LD_a_hldec() //Put value at address HL into A, decrement HL
        {
            cycles += 8;
            SetReg(Registers.A, _core.ReadMem(GetReg(Registers.HL)));
            SetReg(Registers.HL, (ushort)(GetReg(Registers.HL) - 1));
        }
        private void LD_hldec_a() //Put a @ adress in HL, decrement HL
        {
            cycles += 8;
            _core.WriteMem(GetReg(Registers.HL), (byte)GetReg(Registers.A));
            SetReg(Registers.HL, (ushort)(GetReg(Registers.HL) - 1));
        }
        private void LD_a_hlinc() //Put value at address HL into A, decrement HL
        {
            cycles += 8;
            SetReg(Registers.A, _core.ReadMem(GetReg(Registers.HL)));
            SetReg(Registers.HL, (ushort)(GetReg(Registers.HL) + 1));
        }
        private void LD_hlinc_a() //Put a @ adress in HL, decrement HL
        {
            cycles += 8;
            _core.WriteMem(GetReg(Registers.HL), (byte)GetReg(Registers.A));
            SetReg(Registers.HL, (ushort)(GetReg(Registers.HL) + 1));
        }
        private void LDH_n_a() // Put A into 0xff00 + n
        {
            cycles += 12;
            _core.WriteMem((ushort)(0xFF00 + Imm8bitvalue()), (byte)GetReg(Registers.A));
        }
        private void LDH_a_n() // Put mem 0xff00 + n into A
        {
            cycles += 12;
            byte value = (byte)(Imm8bitvalue());
            SetReg(Registers.A, _core.ReadMem((ushort)(0xFF00 + value)));
        }
        private void LD_n_nn(Registers reg) //Put 16bit immidiate value into NN
        {
            cycles += 12;
            SetReg(reg, Imm16bitvalue());
        }
        private void LD_sp_hl() //Put HL into SP
        {
            cycles += 8;
            SetReg(Registers.SP, GetReg(Registers.HL));
        }
        private void LD_hl_sp_n() //Put sp and immidiate value into hl
        {
            cycles += 12;
            ushort sp = GetReg(Registers.SP);
            sbyte imm = (sbyte)GetReg(Registers.n);

            SetFlag(Flags.Carry, (byte)((sp & 0xff) + (imm & 0xff) > 0xff ? 1 : 0));
            SetFlag(Flags.HalfCarry, (byte)(((sp & 0xF) + (imm & 0xF)) > 0xF ? 1 : 0));
            SetFlag(Flags.SubFlag, 0);
            SetFlag(Flags.ZeroFlag, 0);

            SetReg(Registers.HL, (ushort)(sp + imm));

        }
        private void LD_nn_sp() //Put stackpointer at 16bit value
        {
            cycles += 20;
            ushort dest = Imm16bitvalue();
            _core.WriteMem(dest, (byte)(GetReg(Registers.SP) & 0xFF));
            _core.WriteMem((ushort)(dest + 1), (byte)(GetReg(Registers.SP) >> 8));
        }
        private void Push_nn(Registers reg) //Push reg to stack
        {
            cycles += 16;
            SetReg(Registers.SP, (ushort)(GetReg(Registers.SP) - 1));
            _core.WriteMem(GetReg(Registers.SP), (byte)(GetReg(reg) >> 8));
            SetReg(Registers.SP, (ushort)(GetReg(Registers.SP) - 1));
            _core.WriteMem(GetReg(Registers.SP), (byte)(GetReg(reg) & 0xFF));
        }
        private void Pop_nn(Registers reg) //Pop stack to reg
        {

            cycles += 12;
            ushort temp;
            if (reg == Registers.AF)
            {
                temp = (ushort)(_core.ReadMem(GetReg(Registers.SP)) & 0xFF);
                SetReg(Registers.SP, (ushort)(GetReg(Registers.SP) + 1));
                temp |= (ushort)(_core.ReadMem(GetReg(Registers.SP)) << 8);
                SetReg(Registers.SP, (ushort)(GetReg(Registers.SP) + 1));
                SetReg(reg, (ushort)(temp & 0xFFF0));
            }
            else
            {
                temp = (ushort)(_core.ReadMem(GetReg(Registers.SP)) & 0xFF);
                SetReg(Registers.SP, (ushort)(GetReg(Registers.SP) + 1));
                temp |= (ushort)(_core.ReadMem(GetReg(Registers.SP)) << 8);
                SetReg(Registers.SP, (ushort)(GetReg(Registers.SP) + 1));
                SetReg(reg, temp);
            }


        }
        private void Add_a_n(Registers reg) // ADD reg with A
        {
            ushort temp;
            ushort a = GetReg(Registers.A);
            byte value;
            if (reg == Registers.HL)
            {
                cycles += 8;
                temp = _core.ReadMem(GetReg(reg));
            }
            else if (reg == Registers.n)
            {
                cycles += 8;
                temp = Imm8bitvalue();
            }
            else
            {
                cycles += 4;
                temp = GetReg(reg);
            }
            value = (byte)(a + temp);

            SetFlag(Flags.Carry, (byte)((a + temp) > 0xff ? 1 : 0));
            SetFlag(Flags.HalfCarry, (byte)(((a & 0xF) + (temp & 0xF)) > 0xF ? 1 : 0));
            SetFlag(Flags.SubFlag, 0);
            SetFlag(Flags.ZeroFlag, (byte)(value == 0 ? 1 : 0));

            SetReg(Registers.A, value);

        }
        private void Adc_a_n(Registers reg) // ADD reg with A with carry
        {
            ushort temp;
            ushort a = GetReg(Registers.A);
            byte value;
            byte carry = GetFlag(Flags.Carry);
            if (reg == Registers.HL)
            {
                cycles += 8;
                temp = _core.ReadMem(GetReg(reg));
            }
            else if (reg == Registers.n)
            {
                cycles += 8;
                temp = Imm8bitvalue();
            }
            else
            {
                cycles += 4;
                temp = GetReg(reg);
            }
            value = (byte)(a + temp + carry);

            SetFlag(Flags.Carry, (byte)((a + temp + carry) > 0xff ? 1 : 0));
            SetFlag(Flags.HalfCarry, (byte)(((a & 0xF) + (temp & 0xF) + carry) > 0xF ? 1 : 0));
            SetFlag(Flags.SubFlag, 0);
            SetFlag(Flags.ZeroFlag, (byte)(value == 0 ? 1 : 0));

            SetReg(Registers.A, value);

        }
        private void Sub_a_n(Registers reg) // Sub reg with A
        {
            ushort temp;
            ushort a = GetReg(Registers.A);
            byte value;
            if (reg == Registers.HL)
            {
                cycles += 8;
                temp = _core.ReadMem(GetReg(reg));
            }
            else if (reg == Registers.n)
            {
                cycles += 8;
                temp = Imm8bitvalue();
            }
            else
            {
                cycles += 4;
                temp = GetReg(reg);
            }
            value = (byte)(a - temp);

            SetFlag(Flags.Carry, (byte)(a < temp ? 1 : 0));
            SetFlag(Flags.HalfCarry, (byte)((a & 0xF) < (temp & 0xf) ? 1 : 0));
            SetFlag(Flags.SubFlag, 1);
            SetFlag(Flags.ZeroFlag, (byte)(value == 0 ? 1 : 0));

            SetReg(Registers.A, value);

        }
        private void Sbc_a_n(Registers reg) // Sub reg with A with carry Halfcarry fucked for now
        {
            byte a = (byte)GetReg(Registers.A);
            byte carry = GetFlag(Flags.Carry);
            ushort temp;
            int sum;
            byte result;
            if (reg == Registers.HL || reg == Registers.nn)
            {
                cycles += 8;
                temp = _core.ReadMem(GetReg(reg));
                sum = temp + carry;
            }
            else if (reg == Registers.n)
            {
                cycles += 8;
                temp = Imm8bitvalue();
                sum = temp + carry;
            }
            else
            {
                cycles += 4;
                temp = GetReg(reg);
                sum = temp + carry;
            }
            result = (byte)(a - sum);
            if ((a & 0xF) < (temp & 0xf))
                SetFlag(Flags.HalfCarry, 1);
            else if ((a & 0xf) < (sum & 0xf))
                SetFlag(Flags.HalfCarry, 1);
            else if ((a & 0xf) == (temp & 0xf) && ((temp & 0xf) == 0xf) && (carry != 0))
                SetFlag(Flags.HalfCarry, 1);
            else
                SetFlag(Flags.HalfCarry, 0);
            SetFlag(Flags.Carry, (byte)(a < sum ? 1 : 0));
            SetFlag(Flags.SubFlag, 1);
            SetFlag(Flags.ZeroFlag, (byte)(result == 0 ? 1 : 0));

            SetReg(Registers.A, result);

        }
        private void Xor_n(Registers reg) // Xor n with A, result in A
        {
            ushort temp = GetReg(Registers.A);
            if (reg == Registers.HL)
            {
                temp ^= _core.ReadMem(GetReg(Registers.HL));
                cycles += 8;
            }
            else if (reg == Registers.n)
            {
                temp ^= GetReg(Registers.n);
                cycles += 8;
            }
            else
            {
                temp ^= GetReg(reg);
                cycles += 4;
            }
            SetFlag(Flags.Carry, 0);
            SetFlag(Flags.HalfCarry, 0);
            SetFlag(Flags.SubFlag, 0);
            SetFlag(Flags.ZeroFlag, (byte)(temp == 0 ? 1 : 0));

            SetReg(Registers.A, temp);
        }
        private void And_n(Registers reg) // And n with A, result in A;
        {
            ushort temp = GetReg(Registers.A);
            if (reg == Registers.HL)
            {
                temp &= _core.ReadMem(GetReg(Registers.HL));
                cycles += 8;
            }
            else if (reg == Registers.n)
            {
                temp &= GetReg(reg);
                cycles += 8;
            }
            else
            {
                temp &= GetReg(reg);
                cycles += 4;
            }
            SetFlag(Flags.Carry, 0);
            SetFlag(Flags.HalfCarry, 1);
            SetFlag(Flags.SubFlag, 0);
            SetFlag(Flags.ZeroFlag, (byte)(temp == 0 ? 1 : 0));

            SetReg(Registers.A, temp);
        }
        private void Or_n(Registers reg) // Or n with A, result in A;
        {
            ushort temp = GetReg(Registers.A);
            if (reg == Registers.HL)
            {
                temp |= _core.ReadMem(GetReg(Registers.HL));
                cycles += 8;
            }
            else if (reg == Registers.n)
            {
                temp |= GetReg(reg);
                cycles += 8;
            }
            else
            {
                temp |= GetReg(reg);
                cycles += 4;
            }
            SetFlag(Flags.Carry, 0);
            SetFlag(Flags.HalfCarry, 0);
            SetFlag(Flags.SubFlag, 0);
            SetFlag(Flags.ZeroFlag, (byte)(temp == 0 ? 1 : 0));

            SetReg(Registers.A, temp);
        }
        private void Cp_n(Registers reg) // Compare A with n. (A - n, throw away result set flags)
        {
            byte temp = (byte)GetReg(Registers.A);
            byte value;
            if (reg == Registers.HL)
            {
                value = _core.ReadMem(GetReg(reg));
                cycles += 8;
            }
            else if (reg == Registers.n)
            {
                value = (byte)GetReg(reg);
                cycles += 8;
            }
            else
            {
                value = (byte)GetReg(reg);
                cycles += 4;
            }
            SetFlag(Flags.Carry, (byte)(temp < value ? 1 : 0));
            SetFlag(Flags.HalfCarry, (byte)((temp & 0xF) < (value & 0xf) ? 1 : 0));
            SetFlag(Flags.SubFlag, 1);
            SetFlag(Flags.ZeroFlag, (byte)((temp == value) ? 1 : 0));
        }
        private void Inc_n(Registers reg) // Inc reg n
        {
            byte temp;
            if (reg == Registers.HL)
            {
                temp = _core.ReadMem(GetReg(reg));
                temp++;
                _core.WriteMem(GetReg(reg), temp);
                cycles += 12;
            }
            else
            {
                temp = (byte)GetReg(reg);
                temp++;
                SetReg(reg, temp);
                cycles += 4;
            }
            //Carry not affected
            SetFlag(Flags.HalfCarry, (byte)((temp & 0xF) == 0 ? 1 : 0));
            SetFlag(Flags.SubFlag, 0);
            SetFlag(Flags.ZeroFlag, (byte)(temp == 0 ? 1 : 0));



        }
        private void Dec_n(Registers reg) // Decrement reg n
        {
            byte temp;
            if (reg == Registers.HL)
            {
                temp = _core.ReadMem(GetReg(reg));
                temp--;
                _core.WriteMem(GetReg(reg), temp);
                cycles += 12;
            }
            else
            {
                temp = (byte)GetReg(reg);
                temp--;
                SetReg(reg, temp);
                cycles += 4;

            }
            //Carry not affected
            SetFlag(Flags.HalfCarry, (byte)((temp & 0xF) == 0xF ? 1 : 0));
            SetFlag(Flags.SubFlag, 1);
            SetFlag(Flags.ZeroFlag, (byte)(temp == 0 ? 1 : 0));

        }

        //16bit arithmetic
        private void Add_hl_n(Registers reg) //Add n to HL
        {
            cycles += 8;
            ushort hl = GetReg(Registers.HL);
            ushort temp = GetReg(reg);
            SetFlag(Flags.Carry, (byte)((temp + hl) > 0xFFFF ? 1 : 0));
            SetFlag(Flags.HalfCarry, (byte)(((temp & 0xFFF) + (hl & 0xFFF)) > 0xFFF ? 1 : 0));
            SetFlag(Flags.SubFlag, 0);
            //Zeroflag not affected

            SetReg(Registers.HL, (ushort)(hl + temp));
        }
        private void Add_sp_n(Registers reg) //Add # value to SP
        {
            cycles += 16;
            ushort sp = GetReg(Registers.SP);
            sbyte temp = (sbyte)GetReg(reg);

            SetFlag(Flags.Carry, (byte)(((temp & 0xFF) + (sp & 0xFF)) > 0xFF ? 1 : 0));
            SetFlag(Flags.HalfCarry, (byte)(((temp & 0xF) + (sp & 0xF)) > 0xF ? 1 : 0));
            SetFlag(Flags.SubFlag, 0);
            SetFlag(Flags.ZeroFlag, 0);

            SetReg(Registers.SP, (ushort)(temp + sp));
        }
        private void Inc_nn(Registers reg) // Inc reg nn
        {
            ushort temp = GetReg(reg);
            cycles += 8;
            //no flags affected


            SetReg(reg, (ushort)(temp + 1));

        }
        private void Dec_nn(Registers reg) // Dec reg nn
        {
            ushort temp = GetReg(reg);
            cycles += 8;
            //no flags affected


            SetReg(reg, (ushort)(temp - 1));

        }
        private void RLCA() //Rotate left bit 7 in carry and bit0
        {
            cycles += 4;
            byte a = (byte)GetReg(Registers.A);
            byte bit7;
            bit7 = (byte)(a >> 7);
            a = (byte)(a << 1 | bit7);

            SetFlag(Flags.HalfCarry, 0);
            SetFlag(Flags.SubFlag, 0);
            SetFlag(Flags.ZeroFlag, 0);
            SetFlag(Flags.Carry, (byte)(bit7));
            SetReg(Registers.A, a);
        }
        private void RLA() //Rotate left bit 7 in carry and carry to bit 0
        {
            cycles += 4;
            ushort a = GetReg(Registers.A);
            byte carry = GetFlag(Flags.Carry);
            ushort value;
            value = (ushort)(a << 1 | carry & 1);

            SetFlag(Flags.HalfCarry, 0);
            SetFlag(Flags.SubFlag, 0);
            SetFlag(Flags.ZeroFlag, 0);
            SetFlag(Flags.Carry, (byte)((a >> 7) & 1));
            SetReg(Registers.A, value);

        }
        private void RRCA() //Rotate right bit 0 in carry and bit7
        {
            cycles += 4;
            ushort a = GetReg(Registers.A);
            ushort value;
            value = (ushort)(a >> 1 | a << 7);

            SetFlag(Flags.HalfCarry, 0);
            SetFlag(Flags.SubFlag, 0);
            SetFlag(Flags.ZeroFlag, 0);
            SetFlag(Flags.Carry, (byte)(a & 1));
            SetReg(Registers.A, value);
        }
        private void RRA() //Rotate right bit 0 in carry and carry to bit 7
        {
            cycles += 4;
            ushort a = GetReg(Registers.A);
            byte carry = GetFlag(Flags.Carry);
            ushort value;
            value = (ushort)(a >> 1 | (ushort)((carry & 1) == 1 ? 0x80 : 0));

            SetFlag(Flags.HalfCarry, 0);
            SetFlag(Flags.SubFlag, 0);
            SetFlag(Flags.ZeroFlag, 0);
            SetFlag(Flags.Carry, (byte)(a & 1));
            SetReg(Registers.A, value);

        }
        private void Rst_n(ushort value) //Restart
        {
            cycles += 32;
            Push_PC();

            SetReg(Registers.PC, value);
        }
        private void Ret() //pop two bytes from stack & jump
        {
            cycles += 8;
            ushort temp;
            temp = (ushort)(_core.ReadMem(GetReg(Registers.SP)));
            SetReg(Registers.SP, (ushort)(GetReg(Registers.SP) + 1));
            temp |= (ushort)(_core.ReadMem(GetReg(Registers.SP)) << 8);
            SetReg(Registers.SP, (ushort)(GetReg(Registers.SP) + 1));

            SetReg(Registers.PC, temp);
        }
        private void Reti() //pop two bytes from stack & jump, enable interupt.
        {
            interuptsenabled = true;
            Ret();
        }
        private void Push_PC() //Push pc to stack
        {

            SetReg(Registers.SP, (ushort)(GetReg(Registers.SP) - 1));
            _core.WriteMem(GetReg(Registers.SP), (byte)((GetReg(Registers.PC)) >> 8));
            SetReg(Registers.SP, (ushort)(GetReg(Registers.SP) - 1));
            _core.WriteMem(GetReg(Registers.SP), (byte)((GetReg(Registers.PC)) & 0xFF));
        }
        private void Pop_PC() //Pop stack to pc
        {

            ushort temp;
            temp = (ushort)(_core.ReadMem(GetReg(Registers.SP)));
            SetReg(Registers.SP, (ushort)(GetReg(Registers.SP) + 1));
            temp |= (ushort)(_core.ReadMem(GetReg(Registers.SP)) << 8);
            SetReg(Registers.SP, (ushort)(GetReg(Registers.SP) + 1));

            SetReg(Registers.PC, temp);
        }

        // Extended Opcodes
        private void Swap_n(Registers reg) // Swap high and low nibble
        {
            byte temp;
            byte value;
            if (reg == Registers.HL)
            {
                cycles += 16;
                temp = _core.ReadMem(GetReg(reg));
                byte upper = (byte)(temp & 0xF0);
                byte lower = (byte)(temp & 0xF);
                value = (byte)(lower << 4 | upper >> 4);
                _core.WriteMem(GetReg(reg), value);

            }
            else
            {
                cycles += 8;
                temp = (byte)GetReg(reg);
                byte upper = (byte)(temp & 0xF0);
                byte lower = (byte)(temp & 0xF);
                value = (byte)(lower << 4 | upper >> 4);
                SetReg(reg, value);
            }

            SetFlag(Flags.Carry, 0);
            SetFlag(Flags.HalfCarry, 0);
            SetFlag(Flags.SubFlag, 0);
            SetFlag(Flags.ZeroFlag, (byte)(value == 0 ? 1 : 0));


        }
        private void Rlc_n(Registers reg) //Rotate left bit 7 in carry and bit0
        {
            cycles += 8;
            byte temp;
            byte value;
            if (reg == Registers.HL)
            {
                cycles += 8;
                temp = _core.ReadMem(GetReg(reg));
                value = (byte)(temp << 1 | temp >> 7 & 1);
                SetFlag(Flags.Carry, (byte)((temp >> 7) & 1));
                _core.WriteMem(GetReg(reg), value);
            }
            else
            {
                temp = (byte)GetReg(reg);
                value = (byte)(temp << 1 | temp >> 7 & 1);
                SetFlag(Flags.Carry, (byte)((temp >> 7) & 1));
                SetReg(reg, value);
            }

            SetFlag(Flags.HalfCarry, 0);
            SetFlag(Flags.SubFlag, 0);
            SetFlag(Flags.ZeroFlag, (byte)(value == 0 ? 1 : 0));


        }
        private void Rl_n(Registers reg) //Rotate left bit 7 in carry and carry to bit 0
        {
            cycles += 8;
            byte temp;
            byte value;
            byte carry = GetFlag(Flags.Carry);
            if (reg == Registers.HL)
            {
                cycles += 8;
                temp = _core.ReadMem(GetReg(reg));
                value = (byte)(temp << 1 | carry);
                _core.WriteMem(GetReg(reg), value);
            }
            else
            {
                temp = (byte)GetReg(reg);
                value = (byte)(temp << 1 | carry);
                SetReg(reg, value);
            }


            SetFlag(Flags.HalfCarry, 0);
            SetFlag(Flags.SubFlag, 0);
            SetFlag(Flags.ZeroFlag, (byte)(value == 0 ? 1 : 0));
            SetFlag(Flags.Carry, (byte)(temp >> 7));

        }
        private void Rrc_n(Registers reg) //Rotate right bit 0 in carry and bit7
        {

            cycles += 8;
            byte temp;
            byte value;
            if (reg == Registers.HL)
            {
                cycles += 8;
                temp = _core.ReadMem(GetReg(reg));
                value = (byte)(temp >> 1 | temp << 7);
                _core.WriteMem(GetReg(reg), value);
            }
            else
            {
                temp = (byte)GetReg(reg);
                value = (byte)(temp >> 1 | temp << 7);
                SetReg(reg, value);
            }

            SetFlag(Flags.HalfCarry, 0);
            SetFlag(Flags.SubFlag, 0);
            SetFlag(Flags.ZeroFlag, (byte)(value == 0 ? 1 : 0));
            SetFlag(Flags.Carry, (byte)(temp & 1));

        }
        private void Rr_n(Registers reg) //Rotate right bit 0 in carry and carry to bit 7
        {
            cycles += 8;
            byte temp;
            byte value;
            byte carry = GetFlag(Flags.Carry);
            if (reg == Registers.HL)
            {
                cycles += 8;
                temp = _core.ReadMem(GetReg(reg));
                value = (byte)(temp >> 1 | carry << 7);
                _core.WriteMem(GetReg(reg), value);
            }
            else
            {
                temp = (byte)GetReg(reg);
                value = (byte)(temp >> 1 | carry << 7);
                SetReg(reg, value);
            }

            SetFlag(Flags.HalfCarry, 0);
            SetFlag(Flags.SubFlag, 0);
            SetFlag(Flags.ZeroFlag, (byte)(value == 0 ? 1 : 0));
            SetFlag(Flags.Carry, (byte)(temp & 1));

        }
        private void Sla_n(Registers reg) //Shift left into carry, lsb 0
        {
            cycles += 8;
            byte temp;
            byte value;
            if (reg == Registers.HL)
            {
                cycles += 8;
                temp = _core.ReadMem(GetReg(reg));
                value = (byte)(temp << 1);
                _core.WriteMem(GetReg(reg), value);
            }
            else
            {
                temp = (byte)GetReg(reg);
                value = (byte)(temp << 1);
                SetReg(reg, value);
            }

            SetFlag(Flags.HalfCarry, 0);
            SetFlag(Flags.SubFlag, 0);
            SetFlag(Flags.ZeroFlag, (byte)(value == 0 ? 1 : 0));
            SetFlag(Flags.Carry, (byte)(temp >> 7));

        }
        private void Sra_n(Registers reg) //Shift right into carry, MSB doesnt change
        {
            cycles += 8;
            byte temp;
            byte value;
            byte carry = GetFlag(Flags.Carry);
            if (reg == Registers.HL)
            {
                cycles += 8;
                temp = _core.ReadMem(GetReg(reg));
                value = (byte)(temp >> 1 | temp & 0x80);
                _core.WriteMem(GetReg(reg), value);
            }
            else
            {
                temp = (byte)GetReg(reg);
                value = (byte)(temp >> 1 | temp & 0x80);
                SetReg(reg, value);
            }

            SetFlag(Flags.HalfCarry, 0);
            SetFlag(Flags.SubFlag, 0);
            SetFlag(Flags.ZeroFlag, (byte)(value == 0 ? 1 : 0));
            SetFlag(Flags.Carry, (byte)(temp & 1));

        }
        private void Srl_n(Registers reg) //Shift right into carry, MSB set to 0
        {
            cycles += 8;
            byte temp;
            byte value;
            byte carry = GetFlag(Flags.Carry);
            if (reg == Registers.HL)
            {
                cycles += 8;
                temp = _core.ReadMem(GetReg(reg));
                value = (byte)(temp >> 1);
                _core.WriteMem(GetReg(reg), value);
            }
            else
            {
                temp = (byte)GetReg(reg);
                value = (byte)(temp >> 1);
                SetReg(reg, value);
            }

            SetFlag(Flags.HalfCarry, 0);
            SetFlag(Flags.SubFlag, 0);
            SetFlag(Flags.ZeroFlag, (byte)(value == 0 ? 1 : 0));
            SetFlag(Flags.Carry, (byte)(temp & 1));

        }
        private void Bit_n(int bit, Registers reg)
        {
            cycles += 8;
            byte value;
            if (reg == Registers.HL)
            {
                cycles += 8;
                value = (byte)_core.ReadMem(GetReg(reg));
            }
            else
            {
                value = (byte)GetReg(reg);
            }
            SetFlag(Flags.HalfCarry, 1);
            SetFlag(Flags.SubFlag, 0);
            SetFlag(Flags.ZeroFlag, (value & (1 << bit)) == 0 ? (byte)1 : (byte)0);
        }
        private void Set_n(int bit, Registers reg)
        {
            cycles += 8;
            byte value;
            if (reg == Registers.HL)
            {
                cycles += 8;
                value = (byte)_core.ReadMem(GetReg(reg));
                value |= (byte)(1 << bit);
                _core.WriteMem(GetReg(reg), value);
            }
            else
            {
                value = (byte)GetReg(reg);
                value |= (byte)(1 << bit);
                SetReg(reg, value);
            }


        }
        private void Res_n(int bit, Registers reg)
        {
            cycles += 8;
            byte value;
            if (reg == Registers.HL)
            {
                cycles += 8;
                value = (byte)_core.ReadMem(GetReg(reg));
                value &= (byte)~(1 << bit);
                _core.WriteMem(GetReg(reg), value);
            }
            else
            {
                value = (byte)GetReg(reg);
                value &= (byte)~(1 << bit);
                SetReg(reg, value);
            }
        }
        #endregion
    }
}
