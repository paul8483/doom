using Doom.Things;

namespace Doom.Game
{
    public enum MonsterState { Sleep, Chase, Attack, Pain, Die, Dead }

    /// Simplified DOOM monster FSM (A_Look/A_Chase rules, p_enemy.c) driven at
    /// 35 tics/s. All timing lives here; the world only executes commands.
    public sealed class MonsterBrain
    {
        readonly MonsterDef def;
        readonly DoomRandom rng;
        readonly IMonsterWorld world;
        readonly bool ambush;

        public MonsterState State { get; private set; } = MonsterState.Sleep;

        // Sequence playback.
        MonsterSeq seq;
        int seqIdx;
        int ticsLeft;
        bool seqLoop;

        // Chase bookkeeping.
        Dir8 moveDir = Dir8.None;
        int moveCount;
        int reaction;
        bool justAttacked;
        bool justHit;

        public MonsterBrain(MonsterDef def, DoomRandom rng, IMonsterWorld world, bool ambush)
        {
            this.def = def; this.rng = rng; this.world = world; this.ambush = ambush;
            StartSeq(def.Stand, loop: true);
        }

        public void Tick()
        {
            if (State == MonsterState.Dead) return;
            ticsLeft--;
            if (ticsLeft > 0) return;
            AdvanceSeq();
        }

        public void NotifyNoise()
        {
            if (State != MonsterState.Sleep || ambush) return;
            Wake();
        }

        /// Damage landed (world already applied HP).
        public void NotifyDamaged()
        {
            if (State == MonsterState.Die || State == MonsterState.Dead) return;
            justHit = true;
            // Бросок боли — ДО Wake: P_DamageMobj бросает PainChance на каждом уроне,
            // включая будящий; а Wake() тратит rng на выбор направления, что сломало бы
            // детерминизм тестов.
            bool pained = rng.Next() < def.PainChance;
            if (State == MonsterState.Sleep) Wake();
            // P_DamageMobj: `target->reactiontime = 0; // we're awake now...`.
            // ПОСЛЕ Wake: в DOOM reactiontime ставится при спавне (P_SpawnMobj),
            // а у нас — в Wake, который затёр бы обнуление, стой оно раньше.
            reaction = 0;
            if (pained)
            {
                State = MonsterState.Pain;
                StartSeq(def.Pain, loop: false);
                Emit(MonsterSoundCue.Pain, 0);
            }
        }

        public void NotifyKilled()
        {
            if (State == MonsterState.Die || State == MonsterState.Dead) return;
            State = MonsterState.Die;
            world.OnDeathStarted();
            EmitVariant(MonsterSoundCue.Death, def.Sounds?.Death);
            StartSeq(def.Death, loop: false);
        }

        void Wake()
        {
            State = MonsterState.Chase;
            reaction = def.ReactionMoves;
            moveDir = Dir8.None;
            moveCount = 0;
            EmitVariant(MonsterSoundCue.Sight, def.Sounds?.Sight);
            StartSeq(def.Run, loop: true);
        }

        // Invariant: OnSeqEntry/OnSeqFinished must remain the FINAL statement of
        // StartSeq/AdvanceSeq — entry hooks may re-enter StartSeq (e.g. LookThink
        // → Wake → StartSeq(Run)), and any write to seq/seqIdx/ticsLeft after
        // that call would corrupt the freshly started sequence. Likewise, state
        // transitions inside *Think handlers must stay tail-positioned. The same
        // goes for world calls in AttackEntry: each must remain the last effect
        // of its branch — a world callback may synchronously re-enter the brain
        // (infighting wiring makes this real).
        void StartSeq(MonsterSeq s, bool loop)
        {
            seq = s; seqLoop = loop; seqIdx = 0;
            ticsLeft = s.Tics[0];
            world.SetFrame(s.Frames[0]);
            OnSeqEntry();
        }

        void AdvanceSeq()
        {
            seqIdx++;
            if (seqIdx >= seq.Frames.Length)
            {
                if (seqLoop) seqIdx = 0;
                else { OnSeqFinished(); return; }
            }
            ticsLeft = seq.Tics[seqIdx];
            world.SetFrame(seq.Frames[seqIdx]);
            OnSeqEntry();
        }

        void OnSeqEntry()
        {
            switch (State)
            {
                case MonsterState.Sleep: LookThink(); break;
                case MonsterState.Chase: ChaseThink(); break;
                case MonsterState.Attack: AttackEntry(); break;
                // Pain/Die: только тайминг кадров.
            }
        }

        void OnSeqFinished()
        {
            switch (State)
            {
                case MonsterState.Attack:
                case MonsterState.Pain:
                    State = MonsterState.Chase;
                    StartSeq(def.Run, loop: true);
                    break;
                case MonsterState.Die:
                    State = MonsterState.Dead;
                    world.OnBecameCorpse();
                    break;
            }
        }

        void LookThink()
        {
            if (world.CanSeeTarget(frontOnly: true)) Wake();
        }

        void ChaseThink()
        {
            if (reaction > 0) reaction--;
            world.FaceTarget();

            if (justAttacked)
            {
                justAttacked = false;
                NewDir();
                MaybeActiveSound();
                return;
            }

            // Melee first (P_CheckMeleeRange включает видимость).
            if (def.MeleeMod > 0 && world.CanSeeTarget(false) &&
                MonsterRules.InMeleeRange(world.DistanceToTarget(), world.TargetRadiusUnits()))
            {
                EnterAttack();
                return; // A_Chase returns into attack state — no active sound this call
            }

            // Ranged (P_CheckMissileRange: sight, justHit, reaction, дистанционный бросок).
            // A_Chase: пока movecount не исчерпан, попытка дальней атаки пропускается
            // (`if (gameskill < sk_nightmare && !fastparm && actor->movecount)
            //  goto nomissile;`) — залп в среднем раз в ~8 ходов, а не каждый ход.
            if ((def.HitscanCount > 0 || def.HasMissile) && moveCount <= 0 && reaction == 0 &&
                world.CanSeeTarget(false) && MissileRangeCheck())
            {
                justAttacked = true;
                EnterAttack();
                return;
            }

            Move();
            MaybeActiveSound();
        }

        void MaybeActiveSound()
        {
            // A_Chase end: `if (activesound && P_Random() < 3)`
            if (!string.IsNullOrEmpty(def.Sounds?.Active) && rng.Next() < 3)
                Emit(MonsterSoundCue.Active, 0);
        }

        bool MissileRangeCheck()
        {
            if (justHit) { justHit = false; return true; }
            return MonsterRules.CheckMissileRange(rng, world.DistanceToTarget(), def.MeleeMod > 0);
        }

        void EnterAttack()
        {
            State = MonsterState.Attack;
            StartSeq(def.Attack, loop: false);
        }

        void AttackEntry()
        {
            if (seqIdx < def.FireIndex) { world.FaceTarget(); return; }
            if (seqIdx > def.FireIndex) return;
            world.FaceTarget();
            // Огонь: melee в упор приоритетнее (A_TroopAttack), иначе дальняя атака.
            if (def.MeleeMod > 0 && world.CanSeeTarget(false) &&
                MonsterRules.InMeleeRange(world.DistanceToTarget(), world.TargetRadiusUnits()))
            {
                Emit(MonsterSoundCue.MeleeAttack, 0);
                world.MeleeHit(MonsterRules.RollDamage(rng, def.MeleeMod, def.MeleeMult));
            }
            else if (def.HitscanCount > 0)
            {
                Emit(MonsterSoundCue.RangedAttack, 0);
                world.FireHitscan(def.HitscanCount);
            }
            else if (def.HasMissile)
            {
                Emit(MonsterSoundCue.RangedAttack, 0);
                world.LaunchMissile();
            }
            // SARG без дальней атаки за пределами melee «промахивается» — ничего.
        }

        void Emit(MonsterSoundCue cue, int variant) => world.PlaySound(cue, variant);

        void EmitVariant(MonsterSoundCue cue, string[] names)
        {
            if (names == null || names.Length == 0) return;
            int variant = names.Length == 1 ? 0 : rng.Next() % names.Length;
            Emit(cue, variant);
        }

        // A_Chase: `if (--actor->movecount<0 || !P_Move(actor)) P_NewChaseDir(actor);`
        // The decrement is UNCONDITIONAL and, when it goes negative, short-circuits
        // P_Move away — so a move turn takes at most ONE step (on the re-decide
        // turn the step is NewChaseDir's P_TryWalk probe). movecount also burns
        // while door-waiting and when moveDir is None — that IS DOOM behavior.
        void Move()
        {
            if (--moveCount >= 0 && moveDir != Dir8.None)
            {
                var res = world.TryStep(moveDir);
                if (res == StepResult.BlockedByDoor) { world.UseDoor(); return; }
                if (res == StepResult.Moved) return;
            }
            NewDir();
        }

        void NewDir()
        {
            world.TargetDelta(out float dx, out float dy);
            moveDir = ChaseDir.NewChaseDir(dx, dy, moveDir, rng,
                d => world.TryStep(d) == StepResult.Moved, out moveCount);
        }

        public void Capture(
            out MonsterState state, out int seqIndex, out int tics,
            out Dir8 dir, out int moves, out int reactionTime,
            out bool attacked, out bool hit)
        {
            state = State;
            seqIndex = seqIdx;
            tics = ticsLeft;
            dir = moveDir;
            moves = moveCount;
            reactionTime = reaction;
            attacked = justAttacked;
            hit = justHit;
        }

        /// Restore chase bookkeeping after a save load. Sequence frames are
        /// re-applied by the world via <see cref="IMonsterWorld.SetFrame"/> when
        /// the controller restarts the matching sequence for <paramref name="state"/>.
        public void RestoreChaseBookkeeping(
            MonsterState state, int seqIndex, int tics,
            Dir8 dir, int moves, int reactionTime,
            bool attacked, bool hit)
        {
            State = state;
            seqIdx = seqIndex < 0 ? 0 : seqIndex;
            ticsLeft = tics < 0 ? 0 : tics;
            moveDir = dir;
            moveCount = moves;
            reaction = reactionTime < 0 ? 0 : reactionTime;
            justAttacked = attacked;
            justHit = hit;

            // Rebind the active sequence table for the restored state without
            // firing entry hooks (those would re-attack / re-wake).
            seq = state switch
            {
                MonsterState.Sleep => def.Stand,
                MonsterState.Chase => def.Run,
                MonsterState.Attack => def.Attack,
                MonsterState.Pain => def.Pain,
                MonsterState.Die => def.Death,
                _ => def.Death,
            };
            seqLoop = state == MonsterState.Sleep || state == MonsterState.Chase;
            if (seq.Frames != null && seq.Frames.Length > 0)
            {
                if (seqIdx >= seq.Frames.Length) seqIdx = seq.Frames.Length - 1;
                world.SetFrame(seq.Frames[seqIdx]);
            }
        }
    }
}
