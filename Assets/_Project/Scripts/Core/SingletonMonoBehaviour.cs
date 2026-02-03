// ============================================================================
// SingletonMonoBehaviour.cs
// 제네릭 싱글톤 MonoBehaviour 베이스 클래스.
//
// 싱글톤이란?
//   특정 클래스의 인스턴스가 프로그램 전체에서 딱 하나만 존재하도록 보장하는 패턴.
//   어디서든 ClassName.Instance로 접근 가능.
//
// 사용 방법:
//   public class GameManager : SingletonMonoBehaviour<GameManager>
//   {
//       protected override void Awake()
//       {
//           base.Awake();  // 반드시 호출 (싱글톤 초기화)
//           // 추가 초기화 로직...
//       }
//   }
//
//   // 어디서든 접근:
//   GameManager.Instance.DoSomething();
//
// 동작 방식:
//   - Awake()에서 Instance가 이미 있으면 중복 → 자기 자신을 Destroy
//   - Instance가 없으면 자기를 Instance로 등록 + DontDestroyOnLoad
//   - DontDestroyOnLoad: 씬 전환 시에도 파괴되지 않음
//
// 주의:
//   - 자식 클래스에서 Awake()를 override할 때 반드시 base.Awake() 호출
//   - 런타임 중 Instance가 null일 수 있으므로 접근 전 체크 권장
//
// Core 레이어 — Unity 의존 (MonoBehaviour 상속).
// ============================================================================

using UnityEngine;

namespace Hexiege.Core
{
    public abstract class SingletonMonoBehaviour<T> : MonoBehaviour where T : MonoBehaviour
    {
        /// <summary>
        /// 전역 접근 가능한 싱글톤 인스턴스.
        /// null이면 아직 Awake()가 호출되지 않았거나 파괴된 상태.
        /// </summary>
        public static T Instance { get; private set; }

        /// <summary>
        /// 싱글톤 초기화. 중복 인스턴스 방지 + 씬 전환 시 유지.
        /// 자식 클래스에서 override 시 반드시 base.Awake() 호출할 것.
        /// </summary>
        protected virtual void Awake()
        {
            // 이미 다른 인스턴스가 존재하면 → 중복이므로 자기 자신 파괴
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            // 이 인스턴스를 싱글톤으로 등록
            Instance = (T)(MonoBehaviour)this;

            // 씬 전환 시에도 파괴되지 않도록 설정
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// 이 오브젝트가 파괴될 때 Instance 참조를 정리.
        /// 없으면 파괴된 객체에 대한 참조가 남아 NullReferenceException 발생 가능.
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
