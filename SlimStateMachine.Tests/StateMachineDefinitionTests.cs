namespace SlimStateMachine.Tests
{
    /// <summary>
    /// Tests for the instance-based <see cref="StateMachineDefinition{TEntity, TEnum}"/> surface
    /// and the static <see cref="StateMachine{TEntity, TEnum}"/> façade-management API
    /// (Use/Reset/Current and forwarding OnTransition) introduced by issue #9.
    ///
    /// Parallel-safety: the assembly runs test classes in parallel (see MSTestSettings.cs).
    /// Pure instance tests share no static state, so they may safely use Invoice/InvoiceStatus.
    /// Tests that touch the static façade use the dedicated Ticket/TicketStatus pair, which no
    /// other test class references, so they never collide with InvoiceStateMachineTests on the
    /// shared static StateMachine&lt;Invoice, InvoiceStatus&gt; cache.
    /// </summary>
    [TestClass]
    public class StateMachineDefinitionTests
    {
        [TestInitialize]
        public void TestInitialize()
        {
            StateMachine<Ticket, TicketStatus>.Reset();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            StateMachine<Ticket, TicketStatus>.Reset();
        }

        // --- Instance builders (no static state) ---

        private static StateMachineDefinition<Invoice, InvoiceStatus> BuildSimpleInvoiceDefinition()
        {
            return StateMachineDefinition<Invoice, InvoiceStatus>.Build(
                invoice => invoice.Status,
                builder =>
                {
                    builder.SetInitialState(InvoiceStatus.Draft);
                    builder.AllowTransition(InvoiceStatus.Draft, InvoiceStatus.Sent);
                    builder.AllowTransition(
                        InvoiceStatus.Sent,
                        InvoiceStatus.Paid,
                        preCondition: inv => inv.RemainingAmount <= 0,
                        preConditionExpression: "Remaining == 0");
                    builder.AllowTransition(
                        InvoiceStatus.Draft,
                        InvoiceStatus.Cancelled,
                        postAction: inv => inv.CancellationReason = "Cancelled from Draft");
                });
        }

        // --- Façade builder (dedicated Ticket type) ---

        private static StateMachineDefinition<Ticket, TicketStatus> BuildSimpleTicketDefinition()
        {
            return StateMachineDefinition<Ticket, TicketStatus>.Build(
                ticket => ticket.Status,
                builder =>
                {
                    builder.SetInitialState(TicketStatus.Open);
                    builder.AllowTransition(TicketStatus.Open, TicketStatus.InProgress);
                    builder.AllowTransition(TicketStatus.InProgress, TicketStatus.Resolved);
                });
        }

        #region Isolation / parallel-safety

        [TestMethod]
        public void TwoDefinitions_SameTypeArgs_DifferentTransitions_BehaveIndependently()
        {
            // Definition A: Draft -> Sent allowed, Draft -> Cancelled NOT defined.
            var defA = StateMachineDefinition<Invoice, InvoiceStatus>.Build(
                invoice => invoice.Status,
                builder =>
                {
                    builder.SetInitialState(InvoiceStatus.Draft);
                    builder.AllowTransition(InvoiceStatus.Draft, InvoiceStatus.Sent);
                });

            // Definition B: Draft -> Cancelled allowed, Draft -> Sent NOT defined.
            var defB = StateMachineDefinition<Invoice, InvoiceStatus>.Build(
                invoice => invoice.Status,
                builder =>
                {
                    builder.SetInitialState(InvoiceStatus.Draft);
                    builder.AllowTransition(InvoiceStatus.Draft, InvoiceStatus.Cancelled);
                });

            var invoiceForA = new Invoice { Id = 1, Status = InvoiceStatus.Draft };
            var invoiceForB = new Invoice { Id = 2, Status = InvoiceStatus.Draft };

            // A allows Sent but rejects Cancelled
            Assert.IsTrue(defA.CanTransition(invoiceForA, InvoiceStatus.Sent));
            Assert.IsFalse(defA.CanTransition(invoiceForA, InvoiceStatus.Cancelled));

            // B allows Cancelled but rejects Sent (proving no shared static state)
            Assert.IsTrue(defB.CanTransition(invoiceForB, InvoiceStatus.Cancelled));
            Assert.IsFalse(defB.CanTransition(invoiceForB, InvoiceStatus.Sent));

            // And the actual transitions land independently
            Assert.IsTrue(defA.TryTransition(invoiceForA, InvoiceStatus.Sent));
            Assert.AreEqual(InvoiceStatus.Sent, invoiceForA.Status);

            Assert.IsTrue(defB.TryTransition(invoiceForB, InvoiceStatus.Cancelled));
            Assert.AreEqual(InvoiceStatus.Cancelled, invoiceForB.Status);
        }

        #endregion

        #region Instance transition behavior

        [TestMethod]
        public void Instance_TryTransition_HappyPath_UpdatesState()
        {
            var def = BuildSimpleInvoiceDefinition();
            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Draft };

            Assert.IsTrue(def.TryTransition(invoice, InvoiceStatus.Sent));
            Assert.AreEqual(InvoiceStatus.Sent, invoice.Status);
        }

        [TestMethod]
        public void Instance_TryTransition_PreConditionGating_BlocksWhenNotMet()
        {
            var def = BuildSimpleInvoiceDefinition();
            // Sent -> Paid requires RemainingAmount <= 0
            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Sent, TotalAmount = 100, AmountPaid = 50 };

            Assert.IsFalse(def.CanTransition(invoice, InvoiceStatus.Paid));
            Assert.IsFalse(def.TryTransition(invoice, InvoiceStatus.Paid));
            Assert.AreEqual(InvoiceStatus.Sent, invoice.Status);

            // Once condition is met, it succeeds
            invoice.AmountPaid = 100;
            Assert.IsTrue(def.CanTransition(invoice, InvoiceStatus.Paid));
            Assert.IsTrue(def.TryTransition(invoice, InvoiceStatus.Paid));
            Assert.AreEqual(InvoiceStatus.Paid, invoice.Status);
        }

        [TestMethod]
        public void Instance_ForceTransition_BypassesPreCondition()
        {
            var def = BuildSimpleInvoiceDefinition();
            // Sent -> Paid requires RemainingAmount <= 0; this invoice fails that condition
            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Sent, TotalAmount = 100, AmountPaid = 50 };

            Assert.IsFalse(def.TryTransition(invoice, InvoiceStatus.Paid)); // gated
            Assert.IsTrue(def.ForceTransition(invoice, InvoiceStatus.Paid)); // bypassed
            Assert.AreEqual(InvoiceStatus.Paid, invoice.Status);
        }

        [TestMethod]
        public void Instance_Queries_ReflectConfiguration()
        {
            var def = BuildSimpleInvoiceDefinition();

            Assert.AreEqual(InvoiceStatus.Draft, def.InitialState);

            CollectionAssert.AreEquivalent(
                new[] { InvoiceStatus.Sent, InvoiceStatus.Cancelled },
                def.GetDefinedTransitions(InvoiceStatus.Draft).ToList());

            Assert.IsTrue(def.IsFinalState(InvoiceStatus.Paid));
            Assert.IsFalse(def.IsFinalState(InvoiceStatus.Draft));

            var paidInvoice = new Invoice { Id = 1, Status = InvoiceStatus.Paid };
            Assert.IsTrue(def.IsInFinalState(paidInvoice));

            CollectionAssert.AreEqual(
                new[] { InvoiceStatus.Draft, InvoiceStatus.Sent, InvoiceStatus.Paid, InvoiceStatus.Cancelled },
                def.GetAllStates());

            var all = def.GetAllTransitions();
            CollectionAssert.AreEquivalent(
                new[] { InvoiceStatus.Sent, InvoiceStatus.Cancelled },
                all[InvoiceStatus.Draft]);
        }

        #endregion

        #region Per-instance OnTransition

        [TestMethod]
        public void Instance_OnTransition_DoesNotFireForOtherInstance()
        {
            var defA = BuildSimpleInvoiceDefinition();
            var defB = BuildSimpleInvoiceDefinition();

            int aFired = 0;
            int bFired = 0;
            defA.OnTransition += _ => aFired++;
            defB.OnTransition += _ => bFired++;

            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Draft };
            Assert.IsTrue(defA.TryTransition(invoice, InvoiceStatus.Sent));

            Assert.AreEqual(1, aFired);
            Assert.AreEqual(0, bFired); // B's handler must not fire for A's transition
        }

        [TestMethod]
        public void Instance_OnTransition_ContextFieldsAreCorrect()
        {
            var def = BuildSimpleInvoiceDefinition();

            TransitionContext<Invoice, InvoiceStatus>? captured = null;
            def.OnTransition += ctx => captured = ctx;

            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Draft };
            var metadata = new Dictionary<string, object> { ["UserId"] = 7 };

            Assert.IsTrue(def.TryTransition(invoice, InvoiceStatus.Sent, "instance reason", metadata));

            Assert.IsNotNull(captured);
            Assert.AreSame(invoice, captured!.Entity);
            Assert.AreEqual(InvoiceStatus.Draft, captured.FromState);
            Assert.AreEqual(InvoiceStatus.Sent, captured.ToState);
            Assert.AreEqual("instance reason", captured.Reason);
            Assert.AreEqual(7, captured.Metadata!["UserId"]);
            Assert.IsFalse(captured.WasForced);
        }

        [TestMethod]
        public void Instance_OnTransition_WasForced_TrueForForceTransition()
        {
            var def = BuildSimpleInvoiceDefinition();

            TransitionContext<Invoice, InvoiceStatus>? captured = null;
            def.OnTransition += ctx => captured = ctx;

            // Sent -> Paid condition fails, so force it
            var invoice = new Invoice { Id = 1, Status = InvoiceStatus.Sent, TotalAmount = 100, AmountPaid = 50 };
            Assert.IsTrue(def.ForceTransition(invoice, InvoiceStatus.Paid, "admin"));

            Assert.IsNotNull(captured);
            Assert.IsTrue(captured!.WasForced);
            Assert.AreEqual("admin", captured.Reason);
        }

        #endregion

        #region Instance carries no global state

        [TestMethod]
        public void Build_DoesNotMakeStaticCurrentResolve()
        {
            // Clean slate already ensured by TestInitialize's Reset of the Ticket façade.
            _ = BuildSimpleTicketDefinition();

            // Building an instance must NOT register it with the static façade.
            Assert.ThrowsException<InvalidOperationException>(() =>
                _ = StateMachine<Ticket, TicketStatus>.Current);

            Assert.ThrowsException<InvalidOperationException>(() =>
                _ = StateMachine<Ticket, TicketStatus>.InitialState);
        }

        #endregion

        #region Use + façade delegation

        [TestMethod]
        public void Use_ThenFacade_DelegatesToInstance()
        {
            var def = BuildSimpleTicketDefinition();
            StateMachine<Ticket, TicketStatus>.Use(def);

            Assert.AreSame(def, StateMachine<Ticket, TicketStatus>.Current);
            Assert.AreEqual(TicketStatus.Open, StateMachine<Ticket, TicketStatus>.InitialState);

            var ticket = new Ticket { Id = 1, Status = TicketStatus.Open };
            Assert.IsTrue(StateMachine<Ticket, TicketStatus>.TryTransition(ticket, TicketStatus.InProgress));
            Assert.AreEqual(TicketStatus.InProgress, ticket.Status);

            CollectionAssert.AreEquivalent(
                new[] { TicketStatus.InProgress },
                StateMachine<Ticket, TicketStatus>.GetDefinedTransitions(TicketStatus.Open).ToList());
        }

        [TestMethod]
        public void Use_Twice_ThrowsInvalidOperationException()
        {
            StateMachine<Ticket, TicketStatus>.Use(BuildSimpleTicketDefinition());

            Assert.ThrowsException<InvalidOperationException>(() =>
                StateMachine<Ticket, TicketStatus>.Use(BuildSimpleTicketDefinition()));
        }

        [TestMethod]
        public void Configure_ThenUse_ThrowsInvalidOperationException()
        {
            StateMachine<Ticket, TicketStatus>.Configure(
                ticket => ticket.Status,
                builder => builder.SetInitialState(TicketStatus.Open));

            Assert.ThrowsException<InvalidOperationException>(() =>
                StateMachine<Ticket, TicketStatus>.Use(BuildSimpleTicketDefinition()));
        }

        [TestMethod]
        public void Use_AfterReset_SucceedsAgain()
        {
            StateMachine<Ticket, TicketStatus>.Use(BuildSimpleTicketDefinition());
            Assert.IsTrue(StateMachine<Ticket, TicketStatus>.Reset());

            var second = BuildSimpleTicketDefinition();
            StateMachine<Ticket, TicketStatus>.Use(second); // should not throw
            Assert.AreSame(second, StateMachine<Ticket, TicketStatus>.Current);
        }

        #endregion

        #region Reset return value

        [TestMethod]
        public void Reset_ReturnsFalse_WhenNothingConfigured()
        {
            // TestInitialize already reset; a fresh reset should report nothing cleared.
            Assert.IsFalse(StateMachine<Ticket, TicketStatus>.Reset());
        }

        [TestMethod]
        public void Reset_ReturnsTrue_AfterConfigure_AndCurrentThrowsAfterward()
        {
            StateMachine<Ticket, TicketStatus>.Configure(
                ticket => ticket.Status,
                builder => builder.SetInitialState(TicketStatus.Open));

            Assert.IsTrue(StateMachine<Ticket, TicketStatus>.Reset());
            Assert.ThrowsException<InvalidOperationException>(() =>
                _ = StateMachine<Ticket, TicketStatus>.Current);
        }

        [TestMethod]
        public void Reset_ReturnsTrue_AfterUse()
        {
            StateMachine<Ticket, TicketStatus>.Use(BuildSimpleTicketDefinition());
            Assert.IsTrue(StateMachine<Ticket, TicketStatus>.Reset());
        }

        [TestMethod]
        public void Reset_IsIdempotent()
        {
            StateMachine<Ticket, TicketStatus>.Use(BuildSimpleTicketDefinition());
            Assert.IsTrue(StateMachine<Ticket, TicketStatus>.Reset());
            Assert.IsFalse(StateMachine<Ticket, TicketStatus>.Reset()); // second time clears nothing
        }

        #endregion

        #region Current throws when unconfigured

        [TestMethod]
        public void Current_Unconfigured_ThrowsWithNotConfiguredMessage()
        {
            try
            {
                _ = StateMachine<Ticket, TicketStatus>.Current;
                Assert.Fail("Expected InvalidOperationException");
            }
            catch (InvalidOperationException ex)
            {
                Assert.IsTrue(ex.Message.Contains("not been configured"), $"Unexpected message: {ex.Message}");
            }
        }

        #endregion

        #region Static OnTransition forwarding

        [TestMethod]
        public void StaticOnTransition_SubscribeBeforeConfigure_Throws()
        {
            // Subscribe forwards to Current, which throws when unconfigured.
            Assert.ThrowsException<InvalidOperationException>(() =>
                StateMachine<Ticket, TicketStatus>.OnTransition += _ => { });
        }

        [TestMethod]
        public void StaticOnTransition_FiresAfterConfigure()
        {
            StateMachine<Ticket, TicketStatus>.Configure(
                ticket => ticket.Status,
                builder =>
                {
                    builder.SetInitialState(TicketStatus.Open);
                    builder.AllowTransition(TicketStatus.Open, TicketStatus.InProgress);
                });

            TransitionContext<Ticket, TicketStatus>? captured = null;
            StateMachine<Ticket, TicketStatus>.OnTransition += ctx => captured = ctx;

            var ticket = new Ticket { Id = 1, Status = TicketStatus.Open };
            StateMachine<Ticket, TicketStatus>.TryTransition(ticket, TicketStatus.InProgress);

            Assert.IsNotNull(captured);
            Assert.AreEqual(TicketStatus.InProgress, captured!.ToState);
        }

        [TestMethod]
        public void StaticOnTransition_HandlerDiesWithInstanceAfterReset()
        {
            // Configure and subscribe a handler bound to the first instance.
            StateMachine<Ticket, TicketStatus>.Configure(
                ticket => ticket.Status,
                builder =>
                {
                    builder.SetInitialState(TicketStatus.Open);
                    builder.AllowTransition(TicketStatus.Open, TicketStatus.InProgress);
                });

            int oldHandlerFired = 0;
            StateMachine<Ticket, TicketStatus>.OnTransition += _ => oldHandlerFired++;

            // Reset clears the instance; the handler lived on that instance and should die with it.
            Assert.IsTrue(StateMachine<Ticket, TicketStatus>.Reset());

            // Reconfigure with a brand-new instance and transition.
            StateMachine<Ticket, TicketStatus>.Configure(
                ticket => ticket.Status,
                builder =>
                {
                    builder.SetInitialState(TicketStatus.Open);
                    builder.AllowTransition(TicketStatus.Open, TicketStatus.InProgress);
                });

            var ticket = new Ticket { Id = 1, Status = TicketStatus.Open };
            StateMachine<Ticket, TicketStatus>.TryTransition(ticket, TicketStatus.InProgress);

            Assert.AreEqual(0, oldHandlerFired); // old handler must not fire after Reset
        }

        #endregion

        #region Build argument null-checks

        [TestMethod]
        public void Build_NullAccessor_ThrowsArgumentNullException()
        {
            Assert.ThrowsException<ArgumentNullException>(() =>
                StateMachineDefinition<Invoice, InvoiceStatus>.Build(
                    null!,
                    builder => builder.SetInitialState(InvoiceStatus.Draft)));
        }

        [TestMethod]
        public void Build_NullConfigureAction_ThrowsArgumentNullException()
        {
            Assert.ThrowsException<ArgumentNullException>(() =>
                StateMachineDefinition<Invoice, InvoiceStatus>.Build(
                    invoice => invoice.Status,
                    null!));
        }

        #endregion

        #region Instance diagram generation

        [TestMethod]
        public void Instance_GenerateMermaidGraph_ProducesExpectedMarkers()
        {
            var def = BuildSimpleInvoiceDefinition();
            var graph = def.GenerateMermaidGraph();

            Assert.IsTrue(graph.StartsWith("graph TD"));
            Assert.IsTrue(graph.Contains("Start((⚪)) --> Draft"));
            Assert.IsTrue(graph.Contains("Draft --> Sent"));
        }

        [TestMethod]
        public void Instance_GenerateD2Graph_ProducesExpectedMarkers()
        {
            var def = BuildSimpleInvoiceDefinition();
            var graph = def.GenerateD2Graph();

            Assert.IsTrue(graph.Contains("# State Machine: Invoice - InvoiceStatus"));
            Assert.IsTrue(graph.Contains("Start -> Draft"));
        }

        [TestMethod]
        public void Instance_GenerateDiagram_D2_ProducesExpectedMarkers()
        {
            var def = BuildSimpleInvoiceDefinition();
            var diagram = def.GenerateDiagram(StateMachine<Invoice, InvoiceStatus>.DiagramType.D2);

            Assert.IsTrue(diagram.Contains("# State Machine: Invoice - InvoiceStatus"));
        }

        [TestMethod]
        public void Instance_GenerateDiagram_Mermaid_ProducesExpectedMarkers()
        {
            var def = BuildSimpleInvoiceDefinition();
            var diagram = def.GenerateDiagram(StateMachine<Invoice, InvoiceStatus>.DiagramType.Mermaid);

            Assert.IsTrue(diagram.StartsWith("graph TD"));
        }

        #endregion
    }
}
