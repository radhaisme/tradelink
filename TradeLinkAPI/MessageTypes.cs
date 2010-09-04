﻿

namespace TradeLink.API
{
    /// <summary>
    /// TradeLink Message Types used in TradeLink protocol
    /// http://tradelink.googlecode.com/wiki/TradeLinkProtocol
    /// </summary>
    public enum MessageTypes
    {
        // START TRADELINK STATUS MESSAGES - DO NOT REMOVE OR RENAME MESSAGES (only add/insert)
        // IF CHANGED, MUST COPY THIS ENUM'S CONTENTS TO BROKERSERVERS\TRADELIBFAST\TRADELINK.H
        ORDER_NOT_FOUND = -112,
        TLCLIENT_NOT_FOUND = -111,
        INVALID_ACCOUNT = -110,
        UNKNOWN_ERROR = -109,
        FEATURE_NOT_IMPLEMENTED = -108,
        CLIENTNOTREGISTERED = -107,
        EMPTY_ORDER = -106,
        UNKNOWN_MESSAGE = -105,
        UNKNOWN_SYMBOL = -104,
        BROKERSERVER_NOT_FOUND = -103,
        INVALID_ORDERSIZE = -102,
        DUPLICATE_ORDERID = -101,
        SYMBOL_NOT_LOADED = -100,
        OK = 0,
        // END STATUS MESSAGES
        // START CUSTOM MESSAGES  - DO NOT REMOVE OR RENAME MESSAGES
        CUSTOM1 = 1,
        CUSTOM2,
        CUSTOM3,
        CUSTOM4,
        CUSTOM5,
        CUSTOM6,
        CUSTOM7,
        CUSTOM8,
        CUSTOM9,
        CUSTOM10,
        CUSTOM11,
        CUSTOM12,
        CUSTOM13,
        CUSTOM14,
        CUSTOM15,
        CUSTOM16,
        CUSTOM17,
        CUSTOM18,
        CUSTOM19,
        CUSTOM20,
        CUSTOM21,
        CUSTOM22,
        CUSTOM23,
        CUSTOM24,
        CUSTOM25,
        CUSTOM26,
        CUSTOM27,
        CUSTOM28,
        CUSTOM29,
        CUSTOM30,
        CUSTOM31,
        CUSTOM32,
        CUSTOM33,
        CUSTOM34,
        CUSTOM35,
        CUSTOM36,
        CUSTOM37,
        CUSTOM38,
        CUSTOM39,
        CUSTOM40,
        ISSHORTABLE,
        VWAP,
        LASTTRADESIZE,
        LASTTRADEPRICE,
        LASTBID,
        LASTASK,
        BIDSIZE,
        ASKSIZE,
        DAYLOW,
        DAYHIGH,
        OPENPRICE,
        CLOSEPRICE,
        LRPBUY,
        LRPSELL,
        AMEXLASTTRADE,
        NASDAQLASTTRADE,
        NYSEBID,
        NYSEASK,
        NYSEDAYHIGH,
        NYSEDAYLOW,
        NYSELASTTRADE,
        NASDAQIMBALANCE,
        NASDAQPREVIOUSIMBALANCE,
        NYSEIMBALACE,
        NYSEPREVIOUSIMBALANCE,
        POSPRICEREQUEST,
        POSSIZEREQUEST,
        SENDORDEROCO,
        SENDORDEROSO,
        SENDORDERMODIFY,
        SENDORDERPEGMIDPOINT,
        INTRADAYHIGH,
        INTRADAYLOW,
        // END CUSTOM MESSAGES
        // START TRADELINK MESSAGES
        // requests
        SENDORDER = 5000,
        BROKERNAME,
        VERSION,
        REGISTERCLIENT,
        REGISTERSTOCK,
        CLEARSTOCKS,
        CLEARCLIENT,
        HEARTBEATREQUEST,
        ACCOUNTREQUEST,
        ORDERCANCELREQUEST,
        POSITIONREQUEST,
        FEATUREREQUEST,
        BARREQUEST,
        DOMREQUEST,
        IMBALANCEREQUEST,
        SENDORDERMARKET,
        SENDORDERLIMIT,
        SENDORDERSTOP,
        SENDORDERTRAIL,
        SENDORDERMARKETONCLOSE,
        ORDERSTATUSREQUEST,
        ORDERNOTIFYREQUEST,
        // responses or acks
        TICKNOTIFY = 6000,
        EXECUTENOTIFY,
        ORDERNOTIFY,
        ACCOUNTRESPONSE,
        ORDERCANCELRESPONSE,
        FEATURERESPONSE,
        POSITIONRESPONSE,
        IMBALANCERESPONSE,
        DOMRESPONSE,
        LIVEDATA,
        LIVETRADING,
        SIMTRADING,
        HISTORICALDATA,
        HISTORICALTRADING,
        LOOPBACKSERVER,
        LOOPBACKCLIENT,
        STARTHISTORICALRUN,
        ENDHISTORICALRUN,
        SERVERUP,
        SERVERDOWN,
        BARRESPONSE,
        ORDERSTATUSRESPONSE,
        HEARTBEATRESPONSE,
        
        // END TRADELINK MESSAGES - DO NOT REMOVE OR RENAME MESSAGES (ONLY ADD/INSERT)
        // IF CHANGED, MUST COPY THIS ENUM TO BROKERSERVERS\TRADELIBFAST\TRADELINK.H

    }
}