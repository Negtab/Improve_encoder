using System;

namespace ImropveCrypto.Models;

public class Crypto
{
    private readonly bool[] _state; //  28 bits (индексы 0..27)
    private readonly bool[] _lfsr;  // LFSR state,
    private int _bitsEmittedFromInitial;   // сколько бит из initialState уже выдано
    private bool _initialEmitted;          // флаг, что всё начальное состояние выдано

    private readonly int _tap1 = 27; // старший бит (x^28)
    private readonly int _tap2 = 3;  // x^4

    /// <summary>
    /// Инициализирует LFSR начальным состоянием (массив из 28 байт, каждый 0 или 1).
    /// </summary>
    public Crypto(byte[] initialState)
    {
        if (initialState == null || initialState.Length != 28)
            throw new ArgumentException("Initial state must be exactly 28 bytes (0 or 1).");

        _state = new bool[28];
        _lfsr = new bool[28];
        for (int i = 0; i < 28; i++)
        {
            _state[i] = initialState[i] == 1;
            _lfsr[i] = _state[i];
        }
        _bitsEmittedFromInitial = 0;
        _initialEmitted = false;
    }

    /// <summary>
    /// Генерирует один бит, сдвигает регистр и возвращает новый бит.
    /// </summary>
    private bool Shift()
    {
        bool feedback = _lfsr[_tap1] ^ _lfsr[_tap2];
        for (int i = 27; i > 0; i--)
            _lfsr[i] = _lfsr[i - 1];
        _lfsr[0] = feedback;
        return feedback;
    }

    /// <summary>
    /// Возвращает следующий бит ключа (сначала из начального состояния, затем из LFSR).
    /// </summary>
    private bool NextBit()
    {
        if (!_initialEmitted)
        {
            if (_bitsEmittedFromInitial < 28)
            {
                bool bit = _state[_bitsEmittedFromInitial];
                _bitsEmittedFromInitial++;
                if (_bitsEmittedFromInitial == 28)
                    _initialEmitted = true;
                return bit;
            }
            else
            {
                _initialEmitted = true;
            }
        }
        // После выдачи начальных 28 бит – генерируем новые
        return Shift();
    }
    
    /// Генерирует указанное количество байт ключа.
    /// Первые 28 бит (3.5 байта) – это начальное состояние, остальные – сгенерированные.
    public byte[] GenerateKeyBytes(int byteCount)
    {
        byte[] result = new byte[byteCount];
        for (int i = 0; i < byteCount; i++)
        {
            byte b = 0;
            for (int bit = 0; bit < 8; bit++)
            {
                b = (byte)((b << 1) | (NextBit() ? 1 : 0));
            }
            result[i] = b;
        }
        return result;
    }
}