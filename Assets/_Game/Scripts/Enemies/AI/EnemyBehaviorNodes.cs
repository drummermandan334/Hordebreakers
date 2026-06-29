using Unity.Behavior;
using Unity.Properties;
using UnityEngine;

namespace Hordebreakers
{
    // Generic enemy-AI behavior-tree nodes. Each resolves the enemy's IEnemyBody from the agent's GameObject and drives
    // the body's verbs — so ONE node set + ONE graph works for every archetype (Rusher / Charger / Brute / …); the
    // archetype differences live in the IEnemyBody implementation + the EnemyData/EliteData numbers. No blackboard
    // variables: the body holds its own player/data (set in Init), so the graph is purely structural.

    [System.Serializable, GeneratePropertyBag]
    [Condition(name: "In Attack Range", story: "In attack range", category: "Conditions/Hordebreakers", id: "f37624f5-7060-4c86-977c-4db32664f78e")]
    public partial class InAttackRangeCondition : Condition
    {
        private IEnemyBody _body;
        public override bool IsTrue()
        {
            if (_body == null && GameObject != null) _body = GameObject.GetComponent<IEnemyBody>();
            return _body != null && _body.IsAlive && _body.InAttackRange;
        }
    }

    [System.Serializable, GeneratePropertyBag]
    [Condition(name: "Off Cooldown", story: "Off cooldown", category: "Conditions/Hordebreakers", id: "bab23d17-1eda-43b7-b324-d5a74f7d8299")]
    public partial class OffCooldownCondition : Condition
    {
        private IEnemyBody _body;
        public override bool IsTrue()
        {
            if (_body == null && GameObject != null) _body = GameObject.GetComponent<IEnemyBody>();
            return _body != null && _body.IsAlive && _body.OffCooldown;
        }
    }

    [System.Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Approach", story: "Approach until in attack range", category: "Action/Hordebreakers", id: "0fd27a6d-9597-4a36-96af-b8ec1b43b164")]
    public partial class ApproachAction : Action
    {
        private IEnemyBody _body;
        protected override Status OnStart()
        {
            _body = GameObject != null ? GameObject.GetComponent<IEnemyBody>() : null;
            return _body == null ? Status.Failure : Status.Running;
        }
        protected override Status OnUpdate()
        {
            if (_body == null || !_body.IsAlive) return Status.Failure;
            _body.ApproachStep(Time.deltaTime);
            return _body.InAttackRange ? Status.Success : Status.Running;
        }
    }

    [System.Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Reposition", story: "Hold/strafe until ready to attack", category: "Action/Hordebreakers", id: "d6c88f37-9f93-4b52-bb65-d30ccc263bed")]
    public partial class RepositionAction : Action
    {
        private IEnemyBody _body;
        protected override Status OnStart()
        {
            _body = GameObject != null ? GameObject.GetComponent<IEnemyBody>() : null;
            return _body == null ? Status.Failure : Status.Running;
        }
        protected override Status OnUpdate()
        {
            if (_body == null || !_body.IsAlive) return Status.Failure;
            if (_body.OffCooldown) return Status.Success;   // ready — yield so the Selector retries the attack branch
            _body.RepositionStep(Time.deltaTime);
            return Status.Running;
        }
    }

    [System.Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Telegraph", story: "Telegraphed wind-up (aborts if the player escapes / we get hit)", category: "Action/Hordebreakers", id: "8ada4626-7d16-4711-a33a-1aa487ac3cf9")]
    public partial class TelegraphAction : Action
    {
        private IEnemyBody _body;
        protected override Status OnStart()
        {
            _body = GameObject != null ? GameObject.GetComponent<IEnemyBody>() : null;
            if (_body == null) return Status.Failure;
            _body.StartTelegraph();
            return Status.Running;
        }
        protected override Status OnUpdate()
        {
            if (_body == null || !_body.IsAlive) return Status.Failure;
            if (_body.IsStaggered || _body.TelegraphShouldAbort) { _body.CancelTelegraph(); return Status.Failure; }
            return _body.TickTelegraph(Time.deltaTime) ? Status.Success : Status.Running;
        }
    }

    [System.Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Commit Attack", story: "Commit the strike (dash / slam)", category: "Action/Hordebreakers", id: "8c585302-bf58-4300-b6c2-44307637cb24")]
    public partial class CommitAttackAction : Action
    {
        private IEnemyBody _body;
        protected override Status OnStart()
        {
            _body = GameObject != null ? GameObject.GetComponent<IEnemyBody>() : null;
            if (_body == null) return Status.Failure;
            _body.StartAttack();
            return Status.Running;
        }
        protected override Status OnUpdate()
        {
            if (_body == null || !_body.IsAlive) return Status.Failure;
            if (_body.IsStaggered) return Status.Failure;   // a hit interrupted (rusher); brute has poise so never staggers here
            return _body.TickAttack(Time.deltaTime) ? Status.Success : Status.Running;
        }
    }

    [System.Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Recover", story: "Recovery after the strike", category: "Action/Hordebreakers", id: "bff38711-d632-4f2f-a04f-f263349c5987")]
    public partial class RecoverAction : Action
    {
        private IEnemyBody _body;
        protected override Status OnStart()
        {
            _body = GameObject != null ? GameObject.GetComponent<IEnemyBody>() : null;
            if (_body == null) return Status.Failure;
            _body.StartRecover();
            return Status.Running;
        }
        protected override Status OnUpdate()
        {
            if (_body == null || !_body.IsAlive) return Status.Failure;
            return _body.TickRecover(Time.deltaTime) ? Status.Success : Status.Running;
        }
    }
}
