﻿using System;
using System.Collections.Generic;


public class AI : Player 
{
    public AI(string name) : base(name){

    }

    public override bool IsAI
    {
        get{ return true; }
    }


    protected override EResponse OnHandle_TsumoHai(EKaze fromPlayerKaze, Hai haiToHandle)
    {
        _action.Reset();

        if(inTest){
            _action.SutehaiIndex = Utils.GetRandomNum(0, Tehai.getJyunTehaiCount());
            //_action.SutehaiIndex = Tehai.getJyunTehaiCount();
            return DoResponse(EResponse.SuteHai);
        }

        Hai tsumoHai = haiToHandle;

        // ツモあがりの場合は、イベント(ツモあがり)を返す。
        int agariScore = MahjongAgent.getAgariScore(Tehai, tsumoHai, JiKaze);
        if( agariScore > 0 )
        {
            bool hasFuriten = isFuriten();
            if( hasFuriten == false || (hasFuriten && GameSettings.AllowFuriten) )
            {
                return DoResponse(EResponse.Tsumo_Agari);
            }
            else{
                Utils.LogWarningFormat( "AI {0} is enable tsumo but furiten...", JiKaze.ToString() );
            }
        }

        // リーチの場合は、ツモ切りする
        if( MahjongAgent.isReach(JiKaze) )
        {
            _action.SutehaiIndex = Tehai.getJyunTehaiCount();

            return DoResponse(EResponse.SuteHai);
        }

        // check enable Reach
        if( CheckReachPreConditions() == true ) 
        {
            List<int> reachHaiIndexList;
            if( MahjongAgent.tryGetReachHaiIndex(Tehai, tsumoHai, out reachHaiIndexList) )
            {
                _action.IsValidReach = true;
                _action.ReachHaiIndexList = reachHaiIndexList;

                thinkReach();

                return DoResponse(EResponse.Reach);
            }
        }

        // 制限事項。リーチ後のカンをさせない
        if( !MahjongAgent.isReach(JiKaze) ) 
        {
            // TODO: tsumo kans
            List<Hai> kanHais = new List<Hai>();
            if( Tehai.validAnyTsumoKan(tsumoHai, kanHais) )
            {
                _action.setValidTsumoKan(true, kanHais);


            }
        }
        else
        {
            // if player machi hais won't change after setting AnKan, enable to to it.
            if( Tehai.validAnKan(tsumoHai) )
            {
                List<Hai> machiHais;
                if( MahjongAgent.tryGetMachiHais(Tehai, out machiHais) )
                {
                    Tehai tehaiCopy = new Tehai( Tehai );
                    tehaiCopy.setAnKan( tsumoHai );
                    tehaiCopy.Sort();

                    List<Hai> newMachiHais;

                    if( MahjongAgent.tryGetMachiHais(tehaiCopy, out newMachiHais) )
                    {
                        if( machiHais.Count == newMachiHais.Count ){
                            machiHais.Sort( Tehai.Compare );
                            newMachiHais.Sort( Tehai.Compare );

                            bool enableAnkan = true;

                            for( int i = 0; i < machiHais.Count; i++ )
                            {
                                if( machiHais[i].ID != newMachiHais[i].ID ){
                                    enableAnkan = false;
                                    break;
                                }
                            }

                            if( enableAnkan == true )
                            {
                                _action.setValidTsumoKan(true, new List<Hai>(){ tsumoHai });
                                return DoResponse(EResponse.Ankan);
                            }
                        }
                    }
                }
            }

            // can Ron or Ankan, sute hai automatically.
            _action.SutehaiIndex = Tehai.getJyunTehaiCount(); // sute the tsumo hai on Reach

            return DoResponse(EResponse.SuteHai);
        }


        thinkSutehai( tsumoHai );

        return DoResponse(EResponse.SuteHai);
    }

    protected override EResponse OnHandle_KakanHai(EKaze fromPlayerKaze, Hai haiToHandle)
    {
        _action.Reset();

        if(inTest){
            return DoResponse(EResponse.Nagashi);
        }

        Hai kanHai = haiToHandle;

        int agariScore = MahjongAgent.getAgariScore(Tehai, kanHai, JiKaze);
        if( agariScore > 0 )
        {
            bool hasFuriten = isFuriten();
            if( hasFuriten == false || (hasFuriten && GameSettings.AllowFuriten) )
            {
                return DoResponse(EResponse.Ron_Agari);
            }
            else{
                Utils.LogWarningFormat( "AI {0} is enable ron but furiten...", JiKaze.ToString() );
            }
        }

        return DoResponse(EResponse.Nagashi);
    }

    protected override EResponse OnHandle_SuteHai(EKaze fromPlayerKaze, Hai haiToHandle)
    {
        _action.Reset();

        Hai suteHai = haiToHandle;

        // check Ron
        int agariScore = MahjongAgent.getAgariScore(Tehai, suteHai, JiKaze);
        if( agariScore > 0 ) // Ron
        {
            bool hasFuriten = isFuriten();
            if( hasFuriten == false || (hasFuriten && GameSettings.AllowFuriten) )
            {
                return DoResponse(EResponse.Ron_Agari);
            }
            else{
                Utils.LogWarningFormat( "AI {0} is enable to ron but furiten...", JiKaze.ToString() );
            }
        }

        if( MahjongAgent.getTsumoRemain() <= 0 )
            return DoResponse(EResponse.Nagashi);

        if( MahjongAgent.isReach(JiKaze) )
            return DoResponse(EResponse.Nagashi);

        // TODO: Chii, Pon, Kan check


        return DoResponse(EResponse.Nagashi);
    }


    protected override EResponse OnSelect_SuteHai(EKaze fromPlayerKaze, Hai haiToHandle)
    {
        _action.Reset();

        thinkSelectSuteHai();

        return DoResponse(EResponse.SuteHai);
    }



    protected void thinkSutehai(Hai addHai)
    {
        _action.SutehaiIndex = Tehai.getJyunTehaiCount();

        Tehai tehaiCopy = new Tehai( Tehai );

        FormatWorker.setCounterFormat(tehaiCopy, null);
        int maxScore = getCountFormatScore(FormatWorker);

        for( int i = 0; i < tehaiCopy.getJyunTehaiCount(); i++ )
        {
            Hai hai = tehaiCopy.removeJyunTehaiAt(i);

            FormatWorker.setCounterFormat(tehaiCopy, addHai);

            int score = getCountFormatScore( FormatWorker );
            if( score > maxScore )
            {
                maxScore = score;
                _action.SutehaiIndex = i;
            }

            tehaiCopy.insertJyunTehai(i, hai);
        }
    }


    protected void thinkSelectSuteHai()
    {
        thinkSutehai(null);

        if( _action.SutehaiIndex == Tehai.getJyunTehaiCount() )
        {
            _action.SutehaiIndex = Utils.GetRandomNum(0, Tehai.getJyunTehaiCount());
        }
    }

    protected void thinkReach()
    {
        _action.ReachSelectIndex = 0;

        List<int> reachHaiIndex = _action.ReachHaiIndexList;

        Tehai tehaiCopy = new Tehai( Tehai );
        int maxScore = 0;

        for( int i = 0; i < reachHaiIndex.Count; i++ )
        {
            Hai hai = tehaiCopy.removeJyunTehaiAt(i);

            List<Hai> machiiHais;
            if( MahjongAgent.tryGetMachiHais(tehaiCopy, out machiiHais) )
            {
                for( int m = 0; m < machiiHais.Count; m++ )
                {
                    Hai addHai = machiiHais[m];

                    FormatWorker.setCounterFormat(tehaiCopy, addHai);

                    int score = MahjongAgent.getAgariScore( tehaiCopy, addHai, JiKaze );
                    if( score > maxScore )
                    {
                        maxScore = score;
                        _action.ReachSelectIndex = i;
                    }
                }
            }

            tehaiCopy.insertJyunTehai(i, hai);
        }
    }


    protected readonly static int HYOUKA_SHUU = 1;

    protected int getCountFormatScore(CountFormat countFormat)
    {
        int score = 0;
        HaiCounterInfo[] countArr = countFormat.getCounterArray();

        for( int i = 0; i < countArr.Length; i++ ) 
        {
            if( (countArr[i].numKind & Hai.KIND_SHUU) != 0)
                score += countArr[i].count * HYOUKA_SHUU;

            if( countArr[i].count == 2 )
                score += 4;

            if( countArr[i].count >= 3 )
                score += 8;

            if( (countArr[i].numKind & Hai.KIND_SHUU) > 0 )
            {
                if( (i + 1) < countArr.Length && (countArr[i].numKind + 1) == countArr[i + 1].numKind )
                    score += 4;

                if( (i + 2) < countArr.Length && (countArr[i].numKind + 2) == countArr[i + 2].numKind )
                    score += 4;
            }
        }

        return score;
    }

}
