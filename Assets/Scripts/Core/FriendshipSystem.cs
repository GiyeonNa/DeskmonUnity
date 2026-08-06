using UnityEngine;

namespace Deskmon.Core
{
    /// <summary>
    /// 친밀도. index.html의 friendLv() + pet/snack 처리 이식.
    /// 기획서 v4 §6.3 - "친밀도가 진화 엔진".
    ///
    /// 철칙: 친밀도는 절대 하락하지 않는다 (방치 페널티 금지).
    /// 이 클래스에 감소 API가 없는 것은 실수가 아니라 규칙이다.
    ///
    /// 친밀도는 개체가 아니라 폼 단위다(spId:stage 키) - 샤이니와 일반이 공유한다.
    /// </summary>
    public static class FriendshipSystem
    {
        public struct PetResult
        {
            public bool ok;
            /// <summary>얻은 베리 (PET.min~max 랜덤).</summary>
            public int berry;
            public int newLevel;
            public bool leveledUp;
        }

        public struct SnackResult
        {
            public bool ok;
            /// <summary>실패 사유 - 베리 부족.</summary>
            public bool notEnoughBerry;
            public int newLevel;
            public bool leveledUp;
        }

        public static int Points(SaveData save, string speciesId, int stage)
            => save?.Friend(SaveData.Key(speciesId, stage)) ?? 0;

        public static int Level(SaveData save, BalanceData balance, string speciesId, int stage)
            => balance != null ? balance.FriendLevel(Points(save, speciesId, stage)) : 0;

        /// <summary>
        /// 쓰다듬기. index.html ipc.on('pet') - 베리(랜덤) + 친밀도.
        /// 쿨다운(PET.cd 30초)은 개체 단위의 런타임 상태라 Roamer가 관리한다 -
        /// 세이브에 넣으면 껐다 켜는 것으로 초기화되는데, 그걸 막을 이유까지는 없다.
        /// </summary>
        public static PetResult Pet(SaveData save, BalanceData balance, string speciesId, int stage)
        {
            var r = new PetResult();
            if (save == null || balance == null) return r;

            r.berry = Random.Range((int)balance.petBerryGain.x, (int)balance.petBerryGain.y + 1);
            save.berry += r.berry;

            r.leveledUp = AddPoints(save, balance, speciesId, stage, balance.friendPerPet, out r.newLevel);
            r.ok = true;
            return r;
        }

        /// <summary>
        /// 간식. index.html 가방 패널의 간식 버튼 - 베리 소모 + 친밀도 대폭 + 만복 기록.
        /// fed 기록은 버섯쫑(만복 진화)의 조건이 된다.
        /// </summary>
        public static SnackResult Snack(SaveData save, BalanceData balance, string speciesId, int stage)
        {
            var r = new SnackResult();
            if (save == null || balance == null) return r;

            if (save.berry < balance.snackCost) { r.notEnoughBerry = true; return r; }
            save.berry -= balance.snackCost;

            save.SetFed(SaveData.Key(speciesId, stage), true);

            r.leveledUp = AddPoints(save, balance, speciesId, stage, balance.friendPerSnack, out r.newLevel);
            r.ok = true;
            return r;
        }

        /// <summary>포인트 적립. 레벨이 올랐으면 true.</summary>
        static bool AddPoints(SaveData save, BalanceData balance,
                              string speciesId, int stage, int points, out int newLevel)
        {
            string key = SaveData.Key(speciesId, stage);
            int before = balance.FriendLevel(save.Friend(key));

            save.SetFriend(key, save.Friend(key) + points);

            newLevel = balance.FriendLevel(save.Friend(key));
            return newLevel > before;
        }
    }
}
