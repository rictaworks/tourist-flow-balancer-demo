using System;
using System.Globalization;
using TouristFlowBalancer.Core;
using UnityEngine;

namespace TouristFlowBalancer.Infra
{
    /// <summary>
    /// requirements.md 5.8節・6章・10章：保存と日次リセット（関数G persist / resetIfNeeded）。
    /// PlayerPrefsの全キーにオーナーIDを接頭辞として付与する（owner_id自体を除く、唯一の例外）。
    /// WebGLではPlayerPrefsの書き込みはブラウザのIndexedDBへ非同期に反映されるため、
    /// 値を書き込んだ後は必ず <see cref="PlayerPrefs.Save"/> を呼び、保存を明示的に確定する（14.3節）。
    /// </summary>
    public sealed class Persistence
    {
        // 6章のキー一覧。owner_id のみ接頭辞を持たない唯一のキーで、他はすべて "<ownerId>:" を前置する。
        private const string OwnerIdKey = "owner_id";
        private const string LastResetAtKey = "last_reset_at";
        private const string SeasonKey = "season";
        private const string LastSeedKey = "last_seed";
        private const string BestScoreKey = "best_score";

        /// <summary>日次リセット境界の時刻（JST 03:00。5.8節・5.10節）。</summary>
        private const int ResetHourJst = 3;

        private static readonly TimeSpan JstOffset = TimeSpan.FromHours(9);

        /// <summary>不透明なオーナーID。PlayerPrefsの全キー（owner_id自体を除く）の接頭辞になる。</summary>
        public string OwnerId { get; }

        public Persistence()
        {
            OwnerId = LoadOrCreateOwnerId();
        }

        /// <summary>現在時刻をJST（UTC+9固定オフセット）で返す。ブラウザの実行環境のタイムゾーンに依存しない。</summary>
        public static DateTimeOffset NowJst()
        {
            return DateTimeOffset.UtcNow.ToOffset(JstOffset);
        }

        private static string LoadOrCreateOwnerId()
        {
            if (PlayerPrefs.HasKey(OwnerIdKey))
            {
                string existing = PlayerPrefs.GetString(OwnerIdKey);
                if (!string.IsNullOrEmpty(existing))
                {
                    return existing;
                }
            }

            string generated = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(OwnerIdKey, generated);
            PlayerPrefs.Save();
            return generated;
        }

        private string PrefixedKey(string key)
        {
            return OwnerId + ":" + key;
        }

        /// <summary>
        /// リセット判定（5.8節・9.3節）。前回リセット日時（JST）が、現在時刻（JST）から見て
        /// 直近のJST 03:00境界より前であれば（＝前回リセットがまだその境界を越えていなければ）、
        /// オーナーID以外の全キーを削除し、前回リセット日時を現在時刻で更新する。
        /// 前回リセット日時が未保存（初回起動）の場合も、削除対象キーが存在しないだけで
        /// 同じ手順（リセットとみなし前回リセット日時を初期化）を通す。
        /// </summary>
        /// <param name="nowInstant">現在時刻（どのタイムゾーンのオフセットで渡してもよい。内部でJSTへ正規化する）。</param>
        /// <returns>リセット（キー削除）を実行したか。</returns>
        public bool ResetIfNeeded(DateTimeOffset nowInstant)
        {
            DateTimeOffset nowJst = nowInstant.ToOffset(JstOffset);
            DateTimeOffset boundary = MostRecentBoundary(nowJst);

            bool needsReset = true;
            string lastResetAtRaw = PlayerPrefs.GetString(PrefixedKey(LastResetAtKey), string.Empty);
            if (!string.IsNullOrEmpty(lastResetAtRaw)
                && DateTimeOffset.TryParse(
                    lastResetAtRaw,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out DateTimeOffset lastResetAt))
            {
                // 前回リセットが直近の境界より前（＝まだその境界を越えていない）ならリセットする。
                needsReset = lastResetAt < boundary;
            }

            if (needsReset)
            {
                DeleteAllExceptOwnerId();
                PlayerPrefs.SetString(PrefixedKey(LastResetAtKey), nowJst.ToString("o", CultureInfo.InvariantCulture));
                PlayerPrefs.Save();
            }

            return needsReset;
        }

        /// <summary>現在時刻（JST）を引数なしで使う実運用向けオーバーロード。</summary>
        public bool ResetIfNeeded()
        {
            return ResetIfNeeded(NowJst());
        }

        /// <summary>nowJst（JSTへ正規化済み）から見て直近のJST 03:00境界。</summary>
        private static DateTimeOffset MostRecentBoundary(DateTimeOffset nowJst)
        {
            var todayBoundary = new DateTimeOffset(
                nowJst.Year, nowJst.Month, nowJst.Day, ResetHourJst, 0, 0, JstOffset);

            return nowJst >= todayBoundary ? todayBoundary : todayBoundary.AddDays(-1);
        }

        private void DeleteAllExceptOwnerId()
        {
            PlayerPrefs.DeleteKey(PrefixedKey(LastResetAtKey));
            PlayerPrefs.DeleteKey(PrefixedKey(SeasonKey));
            PlayerPrefs.DeleteKey(PrefixedKey(LastSeedKey));
            PlayerPrefs.DeleteKey(PrefixedKey(BestScoreKey));
        }

        /// <summary>
        /// シーズン状態を保存する（関数E手順8・シーズン精算時。5.8節）。
        /// 編集中のプラン（Plan）は保存しない——復元時は空のプランから始まる。
        /// JSON化はUnityの<see cref="JsonUtility"/>のみを使う（外部ライブラリを追加しない）。
        /// </summary>
        public void Save(Season season)
        {
            if (season == null)
            {
                throw new ArgumentNullException(nameof(season));
            }

            string json = JsonUtility.ToJson(season);
            PlayerPrefs.SetString(PrefixedKey(SeasonKey), json);
            PlayerPrefs.Save();
        }

        /// <summary>保存されたシーズン状態を復元する。保存データが無ければnullを返す。</summary>
        public Season Load()
        {
            string json = PlayerPrefs.GetString(PrefixedKey(SeasonKey), string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            return JsonUtility.FromJson<Season>(json);
        }

        /// <summary>
        /// シーズン精算（関数F finalizeSeason）の結果を保存する（5.6節手順3〜5・5.8節）。
        /// ベストスコアは既存より高い場合のみ更新し、直前のシードは常に上書きする。
        /// 進行中のシーズン保存データ（season キー）は精算により不要になるため削除する
        /// （5.6節手順5「進行中の保存データを削除する」）。
        /// </summary>
        /// <param name="seed">このシーズンのシード（「直前のシード」として保存し、再挑戦に使う）。</param>
        /// <param name="score">このシーズンのスコア（満足延べ人数の累計−断念者の累計）。</param>
        public void SaveResult(int seed, int score)
        {
            int currentBest = LoadBestScore();
            if (score > currentBest)
            {
                PlayerPrefs.SetInt(PrefixedKey(BestScoreKey), score);
            }

            PlayerPrefs.SetInt(PrefixedKey(LastSeedKey), seed);
            PlayerPrefs.DeleteKey(PrefixedKey(SeasonKey));
            PlayerPrefs.Save();
        }

        /// <summary>保存済みのベストスコア。無ければ0。</summary>
        public int LoadBestScore()
        {
            return PlayerPrefs.GetInt(PrefixedKey(BestScoreKey), 0);
        }

        /// <summary>保存済みの直前のシード。無ければnull（「同じシードで再挑戦」を選べない）。</summary>
        public int? LoadLastSeed()
        {
            string key = PrefixedKey(LastSeedKey);
            return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : (int?)null;
        }

        /// <summary>進行中のシーズン保存データが存在するか（タイトル画面の「続きから」の表示判定に使う）。</summary>
        public bool HasSavedSeason()
        {
            return PlayerPrefs.HasKey(PrefixedKey(SeasonKey));
        }
    }
}
