﻿#region License Information
/* SimSharp - A .NET port of SimPy, discrete event simulation framework
Copyright (C) 2014  Heuristic and Evolutionary Algorithms Laboratory (HEAL)

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.*/
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SimSharp.Tests {
  [TestClass]
  public class ResourceTest {
    [TestMethod]
    public void TestResource() {
      var start = new DateTime(2014, 4, 1);
      var env = new Environment(start);
      var resource = new Resource(env, capacity: 1);

      Assert.AreEqual(1, resource.Capacity);
      Assert.AreEqual(0, resource.Count);

      var log = new Dictionary<string, DateTime>();
      env.Process(TestResource(env, "a", resource, log));
      env.Process(TestResource(env, "b", resource, log));
      env.Run();

      Assert.AreEqual(start + TimeSpan.FromSeconds(1), log["a"]);
      Assert.AreEqual(start + TimeSpan.FromSeconds(2), log["b"]);
    }
    private IEnumerable<Event> TestResource(Environment env, string name, Resource resource,
      Dictionary<string, DateTime> log) {
      var req = resource.Request();
      yield return req;
      Assert.AreEqual(1, resource.Count);

      yield return env.Timeout(TimeSpan.FromSeconds(1));
      resource.Release(req);

      log.Add(name, env.Now);
    }

    [TestMethod]
    public void TestResourceWithUsing() {
      var start = new DateTime(2014, 4, 1);
      var env = new Environment(start);
      var resource = new Resource(env, capacity: 1);

      Assert.AreEqual(1, resource.Capacity);
      Assert.AreEqual(0, resource.Count);

      var log = new Dictionary<string, DateTime>();
      env.Process(TestResourceWithUsing(env, "a", resource, log));
      env.Process(TestResourceWithUsing(env, "b", resource, log));
      env.Run();

      Assert.AreEqual(start + TimeSpan.FromSeconds(1), log["a"]);
      Assert.AreEqual(start + TimeSpan.FromSeconds(2), log["b"]);
    }
    private IEnumerable<Event> TestResourceWithUsing(Environment env, string name, Resource resource,
      Dictionary<string, DateTime> log) {
      using (var req = resource.Request()) {
        yield return req;
        Assert.AreEqual(1, resource.Count);

        yield return env.Timeout(TimeSpan.FromSeconds(1));
      }
      log.Add(name, env.Now);
    }

    [TestMethod]
    public void TestResourceSlots() {
      var start = new DateTime(2014, 4, 1);
      var env = new Environment(start);
      var resource = new Resource(env, capacity: 3);
      var log = new Dictionary<string, DateTime>();
      for (int i = 0; i < 9; i++)
        env.Process(TestResourceSlots(env, i.ToString(), resource, log));
      env.Run();

      var expected = new Dictionary<string, int> {
        {"0", 0}, {"1", 0}, {"2", 0}, {"3", 1}, {"4", 1}, {"5", 1}, {"6", 2}, {"7", 2}, {"8", 2}
      }.ToDictionary(x => x.Key, x => start + TimeSpan.FromSeconds(x.Value));
      CollectionAssert.AreEqual(expected, log);
    }
    private IEnumerable<Event> TestResourceSlots(Environment env, string name, Resource resource,
      Dictionary<string, DateTime> log) {
      using (var req = resource.Request()) {
        yield return req;
        log.Add(name, env.Now);
        yield return env.Timeout(TimeSpan.FromSeconds(1));
      }
    }

    [TestMethod]
    public void TestResourceContinueAfterInterrupt() {
      var env = new Environment(new DateTime(2014, 4, 1));
      var res = new Resource(env, capacity: 1);
      env.Process(TestResourceContinueAfterInterrupt(env, res));
      var proc = env.Process(TestResourceContinueAfterInterruptVictim(env, res));
      env.Process(TestResourceContinueAfterInterruptInterruptor(env, proc));
      env.Run();
    }

    private IEnumerable<Event> TestResourceContinueAfterInterrupt(Environment env, Resource res) {
      using (var req = res.Request()) {
        yield return req;
        yield return env.Timeout(TimeSpan.FromSeconds(1));
      }
    }

    private IEnumerable<Event> TestResourceContinueAfterInterruptVictim(Environment env, Resource res) {
      var req = res.Request();
      yield return req;
      Assert.IsFalse(req.IsOk);
      env.ActiveProcess.HandleFault();
      yield return req;
      res.Release(req);
      Assert.AreEqual(new DateTime(2014, 4, 1, 0, 0, 1), env.Now);
    }

    private IEnumerable<Event> TestResourceContinueAfterInterruptInterruptor(Environment env, Process proc) {
      proc.Interrupt();
      yield break;
    }

    [TestMethod]
    public void TestResourceReleaseAfterInterrupt() {
      var env = new Environment(new DateTime(2014, 4, 1));
      var res = new Resource(env, capacity: 1);
      env.Process(TestResourceReleaseAfterInterruptBlocker(env, res));
      var victimProc = env.Process(TestResourceReleaseAfterInterruptVictim(env, res));
      env.Process(TestResourceReleaseAfterInterruptInterruptor(env, victimProc));
      env.Run();
    }

    private IEnumerable<Event> TestResourceReleaseAfterInterruptBlocker(Environment env, Resource res) {
      using (var req = res.Request()) {
        yield return req;
        yield return env.Timeout(TimeSpan.FromSeconds(1));
      }
    }

    private IEnumerable<Event> TestResourceReleaseAfterInterruptVictim(Environment env, Resource res) {
      var req = res.Request();
      yield return req;
      Assert.IsFalse(req.IsOk);
      Assert.IsTrue(env.ActiveProcess.HandleFault());
      // Dont wait for the resource
      res.Release(req);
      Assert.AreEqual(new DateTime(2014, 4, 1), env.Now);
    }

    private IEnumerable<Event> TestResourceReleaseAfterInterruptInterruptor(Environment env, Process proc) {
      proc.Interrupt();
      yield break;
    }

    [TestMethod]
    public void TestResourceWithCondition() {
      var env = new Environment();
      var res = new Resource(env, capacity: 1);
      env.Process(TestResourceWithCondition(env, res));
      env.Run();
    }
    private IEnumerable<Event> TestResourceWithCondition(Environment env, Resource res) {
      using (var req = res.Request()) {
        var timeout = env.Timeout(TimeSpan.FromSeconds(1));
        yield return req | timeout;
        Assert.IsTrue(req.IsOk);
        Assert.IsFalse(timeout.IsProcessed);
      }
    }

    [TestMethod]
    public void TestResourceWithPriorityQueue() {
      var env = new Environment(new DateTime(2014, 4, 1));
      var resource = new PriorityResource(env, capacity: 1);
      env.Process(TestResourceWithPriorityQueue(env, 0, resource, 2, 0));
      env.Process(TestResourceWithPriorityQueue(env, 2, resource, 3, 10));
      env.Process(TestResourceWithPriorityQueue(env, 2, resource, 3, 15)); // Test equal priority
      env.Process(TestResourceWithPriorityQueue(env, 4, resource, 1, 5));
      env.Run();
    }
    private IEnumerable<Event> TestResourceWithPriorityQueue(Environment env, int delay, PriorityResource resource, int priority, int resTime) {
      yield return env.Timeout(TimeSpan.FromSeconds(delay));
      var req = resource.Request(priority);
      yield return req;
      Assert.AreEqual(new DateTime(2014, 4, 1) + TimeSpan.FromSeconds(resTime), env.Now);
      yield return env.Timeout(TimeSpan.FromSeconds(5));
      resource.Release(req);
    }

    [TestMethod, Ignore]
    public void TestSortedQueueMaxlen() {
      // Skipped because .net collections doesn't have max capacities
    }

    [TestMethod, Ignore]
    public void TestGetUsers() {
      // Skipped because we do not want to test internal structure of the users and request/release queue
    }

    [TestMethod]
    public void TestPreemtiveResource() {
      var start = new DateTime(2014, 4, 1);
      var env = new Environment(start);
      var res = new PreemptiveResource(env, capacity: 2);
      var log = new Dictionary<DateTime, int>();
      //                                id           d  p
      env.Process(TestPreemtiveResource(0, env, res, 0, 1, log));
      env.Process(TestPreemtiveResource(1, env, res, 0, 1, log));
      env.Process(TestPreemtiveResource(2, env, res, 1, 0, log));
      env.Process(TestPreemtiveResource(3, env, res, 2, 2, log));
      env.Run();

      var expected = new Dictionary<int, int> {
        {5, 0}, {6, 2}, {10, 3}
      }.ToDictionary(x => start + TimeSpan.FromSeconds(x.Key), x => x.Value);
      CollectionAssert.AreEqual(expected, log);
    }
    private IEnumerable<Event> TestPreemtiveResource(int id, Environment env, PreemptiveResource res, int delay, int prio, Dictionary<DateTime, int> log) {
      yield return env.Timeout(TimeSpan.FromSeconds(delay));
      using (var req = res.Request(priority: prio, preempt: true)) {
        yield return req;
        yield return env.Timeout(TimeSpan.FromSeconds(5));
        if (!env.ActiveProcess.HandleFault())
          log.Add(env.Now, id);
      }
    }

    [TestMethod]
    public void TestPreemetiveResourceTimeout() {
      var env = new Environment();
      var res = new PreemptiveResource(env, capacity: 1);
      env.Process(TestPreemtiveResourceTimeoutA(env, res, 1));
      env.Process(TestPreemtiveResourceTimeoutB(env, res, 0));
      env.Run();
    }
    private IEnumerable<Event> TestPreemtiveResourceTimeoutA(Environment env, PreemptiveResource res, int prio) {
      using (var req = res.Request(priority: prio, preempt: true)) {
        yield return req;
        Assert.IsTrue(env.ActiveProcess.HandleFault());
        yield return env.Timeout(TimeSpan.FromSeconds(1));
        Assert.IsFalse(env.ActiveProcess.HandleFault());
      }
    }
    private IEnumerable<Event> TestPreemtiveResourceTimeoutB(Environment env, PreemptiveResource res, int prio) {
      using (var req = res.Request(priority: prio, preempt: true)) {
        yield return req;
      }
    }

    [TestMethod]
    public void TestMixedPreemtion() {
      var start = new DateTime(2014, 4, 2);
      var env = new Environment(start);
      var res = new PreemptiveResource(env, capacity: 2);
      var log = new Dictionary<DateTime, int>();
      env.Process(TestMixedPreemtion(0, env, res, 0, 1, true, log));
      env.Process(TestMixedPreemtion(1, env, res, 0, 1, true, log));
      env.Process(TestMixedPreemtion(2, env, res, 1, 0, false, log));
      env.Process(TestMixedPreemtion(3, env, res, 1, 0, true, log));
      env.Process(TestMixedPreemtion(4, env, res, 2, 2, true, log));
      env.Run();
      var expected = new Dictionary<int, int> {
        {5, 0}, {6, 3}, {10, 2}, {11,4}
      }.ToDictionary(x => start + TimeSpan.FromSeconds(x.Key), x => x.Value);
      CollectionAssert.AreEqual(expected, log);
    }
    private IEnumerable<Event> TestMixedPreemtion(int id, Environment env, PreemptiveResource res, int delay, int prio, bool preempt, Dictionary<DateTime, int> log) {
      yield return env.Timeout(TimeSpan.FromSeconds(delay));
      using (var req = res.Request(priority: prio, preempt: preempt)) {
        yield return req;
        yield return env.Timeout(TimeSpan.FromSeconds(5));
        if (!env.ActiveProcess.HandleFault())
          log.Add(env.Now, id);
      }
    }
  }
}