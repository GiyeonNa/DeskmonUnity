using System.Collections.Generic;

namespace Deskmon.Core
{
    /// <summary>
    /// 방목 상태 관리. index.html의 roamKey/roamSlots/toggleRoam/roamValidate 이식.
    /// 기획서 v4 §6.1 - "잡은 몬스터가 데스크탑에서 함께 산다".
    ///
    /// 순수 로직으로 둔다 - 세이브의 roam 목록만 다루고, 실제 개체 생성/파괴는
    /// RoamManager(씬 쪽)가 이 목록을 보고 한다. 목록이 정본이고 씬은 투영이다.
    /// </summary>
    public static class RoamSystem
    {
        public enum ToggleResult
        {
            /// <summary>방목 시작.</summary>
            Added,
            /// <summary>회수.</summary>
            Removed,
            /// <summary>슬롯 가득.</summary>
            Full,
            /// <summary>보유하지 않은 폼.</summary>
            NotOwned,
        }

        /// <summary>
        /// 방목 키. index.html roamKey()와 같은 표기 - "spId:stage:0|1".
        /// 세이브에 그대로 저장되므로 형식을 바꾸면 Electron판 세이브와 어긋난다.
        /// </summary>
        public static string Key(string speciesId, int stage, bool shiny)
            => $"{speciesId}:{stage}:{(shiny ? 1 : 0)}";

        /// <summary>키 해석. 형식이 어긋나면 false.</summary>
        public static bool TryParse(string key, out string speciesId, out int stage, out bool shiny)
        {
            speciesId = null; stage = 0; shiny = false;
            if (string.IsNullOrEmpty(key)) return false;

            var parts = key.Split(':');
            if (parts.Length != 3) return false;
            if (!int.TryParse(parts[1], out stage)) return false;

            speciesId = parts[0];
            shiny = parts[2] == "1";
            return true;
        }

        /// <summary>
        /// 방목 슬롯 수. index.html:304 - 기본 + (해금 필드 - 1).
        /// 필드가 4개뿐이라 기본 2 기준 최대 5로 자연히 막힌다 (§6.1의 상한).
        /// </summary>
        public static int Slots(SaveData save, BalanceData balance)
        {
            int baseSlots = balance != null ? balance.roamBaseSlots : 2;
            int unlocked = save?.habitats != null ? save.habitats.Count : 1;
            return baseSlots + (unlocked - 1);
        }

        /// <summary>방목 중인가.</summary>
        public static bool IsRoaming(SaveData save, string speciesId, int stage, bool shiny)
            => save != null && save.roam.Contains(Key(speciesId, stage, shiny));

        /// <summary>
        /// 방목 토글. index.html toggleRoam() 이식.
        /// 켤 때만 슬롯/보유 검사를 한다 - 회수는 언제나 된다.
        /// </summary>
        public static ToggleResult Toggle(SaveData save, BalanceData balance,
                                          string speciesId, int stage, bool shiny)
        {
            string key = Key(speciesId, stage, shiny);

            int at = save.roam.IndexOf(key);
            if (at >= 0)
            {
                save.roam.RemoveAt(at);
                return ToggleResult.Removed;
            }

            var c = save.Creature(speciesId);
            int count = shiny ? c.shiny[stage] : c.s[stage];
            if (count < 1) return ToggleResult.NotOwned;

            if (save.roam.Count >= Slots(save, balance)) return ToggleResult.Full;

            save.roam.Add(key);
            return ToggleResult.Added;
        }

        /// <summary>
        /// 목록 정리. index.html roamValidate() - 더는 보유하지 않은 폼을 뺀다.
        /// 진화나 돌려보내기로 마지막 개체가 사라졌을 때 유령 방목이 남지 않게 한다.
        /// </summary>
        public static void Validate(SaveData save)
        {
            if (save?.roam == null) return;

            for (int i = save.roam.Count - 1; i >= 0; i--)
            {
                if (!TryParse(save.roam[i], out string id, out int stage, out bool shiny)
                    || stage < 0 || stage > 2)
                {
                    save.roam.RemoveAt(i);
                    continue;
                }

                var c = save.Creature(id);
                int count = shiny ? c.shiny[stage] : c.s[stage];
                if (count < 1) save.roam.RemoveAt(i);
            }
        }
    }
}
