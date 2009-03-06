﻿using System;
using System.Collections.Generic;
using System.Text;
using TradeLink.API;

namespace TradeLink.Common
{
    public class ImbalanceImpl : Imbalance
    {
        public ImbalanceImpl() { }
        public ImbalanceImpl(string sym, string ex, int size, int time, int psize, int ptime)
        {
            _time = time;
            _size = size;
            _sym = sym;
            _ex = ex;
            _ptime = ptime;
            _psize = psize;
        }
        string _sym = "";
        string _ex = "NYSE";
        int _size = 0;
        int _psize = 0;
        int _time = 0;
        int _ptime = 0;
        public bool isValid { get { return (_sym != ""); } }
        public bool hasImbalance { get { return _size != 0; } }
        public bool hadImbalance { get { return _psize != 0; } }
        public string Symbol { get { return _sym; } }
        public string Exchange { get { return _ex; } }
        public int ThisImbalance { get { return _size; } }
        public int PrevImbalance { get { return _psize; } }
        public int ThisTime { get { return _time; } }
        public int PrevTime { get { return _ptime; } }

        public static Imbalance Deserialize(string msg)
        {
            ImbalanceImpl i = new ImbalanceImpl();
            string[] r = msg.Split(',');
            i._size= Convert.ToInt32(r[(int)ImbalanceField.IF_SIZE]);
            i._time= Convert.ToInt32(r[(int)ImbalanceField.IF_TIME]);
            i._psize= Convert.ToInt32(r[(int)ImbalanceField.IF_PSIZE]);
            i._ptime= Convert.ToInt32(r[(int)ImbalanceField.IF_PTIME]);
            i._sym= r[(int)ImbalanceField.IF_SYM];
            i._ex= r[(int)ImbalanceField.IF_EX];
            return i;
        }

        public static string Serialize(Imbalance i)
        {
            string[] r = new string[] { i.Symbol, i.Exchange, i.ThisImbalance.ToString(), i.ThisTime.ToString(), i.PrevImbalance.ToString(), i.PrevTime.ToString() };
            return string.Join(",", r);
        }
    }
}