# -*- coding: utf-8 -*-
"""
selection_engine.py
All intelligent selection and layout algorithms extracted from Tag Factory v7.
Shared between TagFactory panel and ISO_Tagging scripts.
IronPython 2.7 compatible.
"""

import math, random, copy
from Autodesk.Revit.DB import (
    FilteredElementCollector, IndependentTag, ElementId, CategoryType
)
from System.Collections.Generic import List


# ─────────────────────────────────────────────────────────────────────────────
# VECTOR MATH
# ─────────────────────────────────────────────────────────────────────────────
class Vec2:
    __slots__ = ['x', 'y']
    def __init__(self, x=0, y=0): self.x, self.y = float(x), float(y)
    def __add__(self, o): return Vec2(self.x + o.x, self.y + o.y)
    def __sub__(self, o): return Vec2(self.x - o.x, self.y - o.y)
    def __mul__(self, s): return Vec2(self.x * s, self.y * s)
    def __truediv__(self, s): return Vec2(self.x / s, self.y / s) if s else Vec2()
    def mag(self): return math.sqrt(self.x*self.x + self.y*self.y)
    def mag_sq(self): return self.x*self.x + self.y*self.y
    def dist(self, o): return (self - o).mag()
    def dist_sq(self, o): return (self - o).mag_sq()
    def norm(self):
        m = self.mag()
        return Vec2(self.x/m, self.y/m) if m > 1e-10 else Vec2()
    def angle(self): return math.atan2(self.y, self.x)
    def copy(self): return Vec2(self.x, self.y)


# ─────────────────────────────────────────────────────────────────────────────
# SPATIAL DATA STRUCTURES
# ─────────────────────────────────────────────────────────────────────────────
class QuadTree:
    MAX_OBJ, MAX_LVL = 8, 6
    def __init__(self, bounds, lvl=0):
        self.bounds, self.lvl, self.objs, self.nodes = bounds, lvl, [], [None]*4
    def clear(self):
        self.objs = []
        for i in range(4):
            if self.nodes[i]: self.nodes[i].clear(); self.nodes[i] = None
    def split(self):
        x, y, w, h = self.bounds
        sw, sh = w/2, h/2
        self.nodes = [QuadTree((x+sw,y,sw,sh),self.lvl+1), QuadTree((x,y,sw,sh),self.lvl+1),
                      QuadTree((x,y+sh,sw,sh),self.lvl+1), QuadTree((x+sw,y+sh,sw,sh),self.lvl+1)]
    def get_idx(self, p):
        x,y,w,h = self.bounds
        return (1 if p.x < x+w/2 else 0) if p.y < y+h/2 else (2 if p.x < x+w/2 else 3)
    def insert(self, obj, pos):
        if self.nodes[0]: self.nodes[self.get_idx(pos)].insert(obj, pos); return
        self.objs.append((obj, pos))
        if len(self.objs) > self.MAX_OBJ and self.lvl < self.MAX_LVL:
            if not self.nodes[0]: self.split()
            for o,p in self.objs: self.nodes[self.get_idx(p)].insert(o, p)
            self.objs = []
    def query_radius(self, pos, r):
        res = []
        x,y,w,h = self.bounds
        cx, cy = max(x, min(pos.x, x+w)), max(y, min(pos.y, y+h))
        if (pos.x-cx)**2 + (pos.y-cy)**2 > r*r: return res
        for obj, p in self.objs:
            if pos.dist_sq(p) <= r*r: res.append((obj, p))
        if self.nodes[0]:
            for n in self.nodes: res.extend(n.query_radius(pos, r))
        return res


class DBSCAN:
    def __init__(self, pts, eps=1.0, min_pts=3):
        self.pts, self.eps, self.min_pts = pts, eps, min_pts
        self.labels = [-1] * len(pts)
    def _region_query(self, idx):
        return [i for i, p in enumerate(self.pts) if self.pts[idx].dist(p) <= self.eps]
    def run(self):
        cluster_id = 0
        for i in range(len(self.pts)):
            if self.labels[i] != -1: continue
            neighbors = self._region_query(i)
            if len(neighbors) < self.min_pts: continue
            self.labels[i] = cluster_id
            seed_set = list(neighbors)
            j = 0
            while j < len(seed_set):
                q = seed_set[j]
                if self.labels[q] == -1:
                    self.labels[q] = cluster_id
                elif self.labels[q] != -1:
                    j += 1; continue
                self.labels[q] = cluster_id
                q_n = self._region_query(q)
                if len(q_n) >= self.min_pts: seed_set.extend(q_n)
                j += 1
            cluster_id += 1
        clusters = {}
        for i, label in enumerate(self.labels):
            clusters.setdefault(label, []).append(i)
        return clusters


class ProximityGraph:
    def __init__(self, pts, threshold=2.0):
        self.pts, self.threshold = pts, threshold
        self.adj = {i: [] for i in range(len(pts))}
    def build(self):
        n = len(self.pts)
        for i in range(n):
            for j in range(i+1, n):
                if self.pts[i].dist(self.pts[j]) <= self.threshold:
                    self.adj[i].append(j); self.adj[j].append(i)
    def find_component_of(self, idx):
        visited, stack = set(), [idx]
        while stack:
            node = stack.pop()
            if node in visited: continue
            visited.add(node); stack.extend(self.adj[node])
        return list(visited)


class GridDetector:
    def __init__(self, pts, tolerance=0.5):
        self.pts, self.tol = pts, tolerance
    def detect(self):
        if len(self.pts) < 4: return None, None
        xs = sorted(set(round(p.x / self.tol) * self.tol for p in self.pts))
        ys = sorted(set(round(p.y / self.tol) * self.tol for p in self.pts))
        x_sp = [xs[i+1]-xs[i] for i in range(len(xs)-1)] if len(xs)>1 else []
        y_sp = [ys[i+1]-ys[i] for i in range(len(ys)-1)] if len(ys)>1 else []
        x_grid = None
        if x_sp:
            avg = sum(x_sp)/len(x_sp)
            if all(abs(s-avg) < self.tol for s in x_sp): x_grid = avg
        y_grid = None
        if y_sp:
            avg = sum(y_sp)/len(y_sp)
            if all(abs(s-avg) < self.tol for s in y_sp): y_grid = avg
        return x_grid, y_grid
    def on_grid(self, xg, yg):
        if not xg and not yg: return list(range(len(self.pts)))
        result = []
        for i, p in enumerate(self.pts):
            on_x = True if not xg else (abs(p.x % xg) < self.tol or abs(p.x % xg - xg) < self.tol)
            on_y = True if not yg else (abs(p.y % yg) < self.tol or abs(p.y % yg - yg) < self.tol)
            if on_x and on_y: result.append(i)
        return result


class BoundaryDetector:
    def __init__(self, pts, margin_pct=0.15):
        self.pts, self.margin = pts, margin_pct
    def find_edge_elements(self):
        if not self.pts: return {'top':[],'bottom':[],'left':[],'right':[],'center':[]}
        min_x = min(p.x for p in self.pts); max_x = max(p.x for p in self.pts)
        min_y = min(p.y for p in self.pts); max_y = max(p.y for p in self.pts)
        mx = (max_x-min_x)*self.margin; my = (max_y-min_y)*self.margin
        result = {'top':[],'bottom':[],'left':[],'right':[],'center':[]}
        for i, p in enumerate(self.pts):
            edge = False
            if p.y >= max_y-my: result['top'].append(i); edge = True
            if p.y <= min_y+my: result['bottom'].append(i); edge = True
            if p.x <= min_x+mx: result['left'].append(i); edge = True
            if p.x >= max_x-mx: result['right'].append(i); edge = True
            if not edge: result['center'].append(i)
        return result


# ─────────────────────────────────────────────────────────────────────────────
# OPTIMISATION ALGORITHMS
# ─────────────────────────────────────────────────────────────────────────────
class KMeans:
    def __init__(self, pts, k=None):
        self.pts = pts
        self.k = k or max(1, min(6, int(math.sqrt(len(pts)/2)))) if pts else 1
    def run(self, iters=50):
        if len(self.pts) < self.k: self.k = max(1, len(self.pts))
        if not self.pts: return [], []
        centroids = [p.copy() for p in random.sample(self.pts, self.k)]
        clusters = []
        for _ in range(iters):
            clusters = [[] for _ in range(self.k)]
            for p in self.pts:
                idx = min(range(self.k), key=lambda i: p.dist_sq(centroids[i]))
                clusters[idx].append(p)
            moved = False
            for i in range(self.k):
                if clusters[i]:
                    nc = Vec2(sum(p.x for p in clusters[i])/len(clusters[i]),
                              sum(p.y for p in clusters[i])/len(clusters[i]))
                    if nc.dist(centroids[i]) > 0.01: moved = True
                    centroids[i] = nc
            if not moved: break
        return centroids, clusters


class SimAnneal:
    def __init__(self, tags, spacing=1.0):
        self.tags, self.spacing = tags, spacing
        self.temp, self.cool, self.min_t = 100.0, 0.995, 0.1
    def energy(self, pos=None):
        pos = pos or [t['pos'] for t in self.tags]
        e = 0
        n = len(pos)
        for i in range(n):
            for j in range(i+1, n):
                d = pos[i].dist(pos[j])
                if d < self.spacing: e += (self.spacing - d)**2 * 100
            d = pos[i].dist(self.tags[i]['elem'])
            if d > self.spacing * 3: e += (d - self.spacing * 3)**2
        return e
    def run(self, iters=500):
        cur = [t['pos'].copy() for t in self.tags]
        cur_e = self.energy(cur)
        best, best_e = [p.copy() for p in cur], cur_e
        for _ in range(iters):
            if self.temp < self.min_t: break
            new = [p.copy() for p in cur]
            idx = random.randint(0, len(new)-1)
            ang = random.uniform(0, 2*math.pi)
            d = random.uniform(0.1, self.spacing*0.5) * (self.temp/100)
            new[idx] = new[idx] + Vec2(math.cos(ang), math.sin(ang)) * d
            new_e = self.energy(new)
            if new_e < cur_e or random.random() < math.exp((cur_e - new_e) / self.temp):
                cur, cur_e = new, new_e
                if cur_e < best_e: best, best_e = [p.copy() for p in cur], cur_e
            self.temp *= self.cool
        for i, t in enumerate(self.tags): t['pos'] = best[i]
        return best_e


class GeneticAlg:
    def __init__(self, tags, spacing=1.0):
        self.tags, self.spacing = tags, spacing
        self.pop_sz, self.elite, self.mut_rate, self.gens = 30, 5, 0.15, 50
    def create_ind(self):
        ind = []
        for t in self.tags:
            ang = random.uniform(0, 2*math.pi)
            d = random.uniform(self.spacing*0.5, self.spacing*2)
            ind.append(t['elem'] + Vec2(math.cos(ang), math.sin(ang)) * d)
        return ind
    def fitness(self, ind):
        score = 1000.0
        n = len(ind)
        for i in range(n):
            for j in range(i+1, n):
                d = ind[i].dist(ind[j])
                if d < self.spacing: score -= (self.spacing - d) * 50
            d = ind[i].dist(self.tags[i]['elem'])
            if d > self.spacing * 3: score -= (d - self.spacing * 3) * 10
            elif d < self.spacing * 0.3: score -= (self.spacing * 0.3 - d) * 20
        return max(0, score)
    def run(self):
        pop = [self.create_ind() for _ in range(self.pop_sz)]
        for _ in range(self.gens):
            scored = sorted([(self.fitness(i), i) for i in pop], key=lambda x: -x[0])
            new_pop = [copy.deepcopy(scored[i][1]) for i in range(self.elite)]
            while len(new_pop) < self.pop_sz:
                p1 = max(random.sample(scored[:self.pop_sz//2], 3), key=lambda x: x[0])[1]
                p2 = max(random.sample(scored[:self.pop_sz//2], 3), key=lambda x: x[0])[1]
                pt = random.randint(1, len(p1)-1)
                child = [p.copy() for p in p1[:pt]] + [p.copy() for p in p2[pt:]]
                for i in range(len(child)):
                    if random.random() < self.mut_rate:
                        child[i] = child[i] + Vec2(random.uniform(-0.3,0.3), random.uniform(-0.3,0.3))
                new_pop.append(child)
            pop = new_pop
        best = max([(self.fitness(i), i) for i in pop], key=lambda x: x[0])[1]
        for i, t in enumerate(self.tags): t['pos'] = best[i]


class ForceEngine:
    def __init__(self, tags, spacing=1.0):
        self.tags, self.spacing, self.damp = tags, spacing, 0.8
    def run(self, iters=50):
        n = len(self.tags)
        if n < 2: return
        for _ in range(iters):
            forces = [Vec2() for _ in self.tags]
            for i, t in enumerate(self.tags):
                for j, t2 in enumerate(self.tags):
                    if i != j:
                        d = t['pos'].dist(t2['pos'])
                        if d < self.spacing * 1.5 and d > 0.001:
                            f = (t['pos'] - t2['pos']).norm() * (self.spacing * 1.5 - d)
                            forces[i] = forces[i] + f
                to_elem = t['elem'] - t['pos']
                d = to_elem.mag()
                if d > self.spacing * 0.5:
                    forces[i] = forces[i] + to_elem.norm() * (d - self.spacing * 0.5) * 0.3
            for i, t in enumerate(self.tags):
                if forces[i].mag() > 0.01: t['pos'] = t['pos'] + forces[i] * self.damp
            self.damp *= 0.98


class LayoutScorer:
    def __init__(self, tags, spacing=1.0): self.tags, self.spacing = tags, spacing
    def score(self):
        if len(self.tags) < 2: return 100, {}
        pos = [t['pos'] for t in self.tags]
        n, scores = len(pos), {}
        overlaps = sum(1 for i in range(n) for j in range(i+1,n) if pos[i].dist(pos[j]) < self.spacing)
        scores['overlap'] = max(0, 30 - (overlaps / max(1, n*(n-1)/2)) * 60)
        cx, cy = sum(p.x for p in pos)/n, sum(p.y for p in pos)/n
        center = Vec2(cx, cy)
        dists = [p.dist(center) for p in pos]
        avg = sum(dists)/n
        var = sum((d-avg)**2 for d in dists)/n
        scores['distribution'] = max(0, 20 - min(20, var * 2))
        total = sum(t['pos'].dist(t['elem']) for t in self.tags)
        scores['leader'] = max(0, 20 - abs(total/n - self.spacing) * 10)
        xs = [round(p.x, 1) for p in pos]; ys = [round(p.y, 1) for p in pos]
        scores['align'] = min(15, (max(xs.count(x) for x in set(xs)) + max(ys.count(y) for y in set(ys)) - 2) * 3)
        if n > 2:
            min_ds = [min(pos[i].dist(pos[j]) for j in range(n) if j != i) for i in range(n)]
            avg_m = sum(min_ds)/n
            scores['uniform'] = max(0, 15 - sum((d-avg_m)**2 for d in min_ds)/n * 5)
        else: scores['uniform'] = 15
        return round(sum(scores.values()), 1), scores


class PatternLearner:
    def __init__(self): self.pattern = None; self.ptype = None
    def learn(self, tags):
        if len(tags) < 2: return 'Need 2+ tags'
        offsets = [(t['pos'] - t['elem']).angle() for t in tags]
        dists = [(t['pos'] - t['elem']).mag() for t in tags]
        ang_var = sum((a - sum(offsets)/len(offsets))**2 for a in offsets) / len(offsets)
        if ang_var < 0.1:
            self.ptype = 'uniform_dir'
            self.pattern = {'angle': sum(offsets)/len(offsets), 'dist': sum(dists)/len(dists)}
        else:
            self.ptype = 'custom'
            self.pattern = {'offsets': offsets, 'dists': dists}
        return 'Learned: ' + self.ptype
    def apply(self, tags):
        if not self.pattern: return 'No pattern'
        if self.ptype == 'uniform_dir':
            for t in tags:
                t['pos'] = t['elem'] + Vec2(math.cos(self.pattern['angle']),
                                            math.sin(self.pattern['angle'])) * self.pattern['dist']
        return 'Applied to {} tags'.format(len(tags))


# ─────────────────────────────────────────────────────────────────────────────
# SMART ORGANIZER
# ─────────────────────────────────────────────────────────────────────────────
class SmartOrganizer:
    PASSES = ['Analyze', 'Cluster', 'Expand', 'Physics', 'Anneal', 'Leaders', 'Polish', 'Score']
    def __init__(self, doc, view, spacing=1.0, iters=50):
        self.doc, self.view, self.spacing, self.iters = doc, view, spacing, iters
        self.pass_num, self.tags, self.init_score = 0, [], 0
    def load(self, tags):
        self.tags = []
        for tag in tags:
            try:
                h = tag.TagHeadPosition
                ep = self._elem_pos(tag)
                self.tags.append({'tag': tag, 'pos': Vec2(h.X, h.Y), 'orig': Vec2(h.X, h.Y),
                                   'elem': ep or Vec2(h.X, h.Y), 'z': h.Z})
            except: pass
        self.init_score = LayoutScorer(self.tags, self.spacing).score()[0]
        return len(self.tags)
    def _elem_pos(self, tag):
        try:
            refs = tag.GetTaggedLocalElements()
            if refs:
                bb = list(refs)[0].get_BoundingBox(self.view)
                if bb: return Vec2((bb.Min.X+bb.Max.X)/2, (bb.Min.Y+bb.Max.Y)/2)
        except: pass
        return None
    def clashes(self):
        return sum(1 for i in range(len(self.tags)) for j in range(i+1, len(self.tags))
                   if self.tags[i]['pos'].dist(self.tags[j]['pos']) < self.spacing)
    def run_pass(self):
        if self.pass_num >= len(self.PASSES): return 'Complete!'
        name = self.PASSES[self.pass_num]
        before = self.clashes()
        if self.pass_num == 0:
            km = KMeans([t['pos'] for t in self.tags])
            self.centroids, self.clusters = km.run()
            result = 'PASS 1: {}\nTags: {}\nClusters: {}\nClashes: {}'.format(
                name, len(self.tags), len([c for c in self.clusters if c]), before)
        elif self.pass_num == 1:
            for i, cluster in enumerate(self.clusters):
                if len(cluster) > 1:
                    center = self.centroids[i]
                    for point in cluster:
                        for t in self.tags:
                            if t['pos'].dist(point) < 0.01:
                                d = t['pos'] - center
                                if d.mag() < 0.01: d = Vec2(random.uniform(-1,1), random.uniform(-1,1))
                                t['pos'] = t['pos'] + d.norm() * self.spacing * 0.3
            result = 'PASS 2: {}\nClashes: {} -> {}'.format(name, before, self.clashes())
        elif self.pass_num == 2:
            cx = sum(t['pos'].x for t in self.tags) / len(self.tags)
            cy = sum(t['pos'].y for t in self.tags) / len(self.tags)
            center = Vec2(cx, cy)
            for t in self.tags:
                d = t['pos'] - center
                if d.mag() < 0.01: d = Vec2(random.uniform(-1,1), random.uniform(-1,1))
                t['pos'] = t['pos'] + d.norm() * self.spacing * 0.4
            result = 'PASS 3: {}\nClashes: {} -> {}'.format(name, before, self.clashes())
        elif self.pass_num == 3:
            ForceEngine(self.tags, self.spacing).run(self.iters)
            result = 'PASS 4: {}\nClashes: {} -> {}'.format(name, before, self.clashes())
        elif self.pass_num == 4:
            SimAnneal(self.tags, self.spacing).run(self.iters * 5)
            result = 'PASS 5: {}\nClashes: {} -> {}'.format(name, before, self.clashes())
        elif self.pass_num == 5:
            count = 0
            for t in self.tags:
                d = t['pos'] - t['elem']
                if d.mag() > 0.01:
                    ang = round(d.angle() / (math.pi/4)) * (math.pi/4)
                    new = Vec2(t['elem'].x + d.mag() * math.cos(ang), t['elem'].y + d.mag() * math.sin(ang))
                    if all(new.dist(o['pos']) >= self.spacing * 0.7 for o in self.tags if o is not t):
                        t['pos'] = new; count += 1
            result = 'PASS 6: {}\nSnapped: {}'.format(name, count)
        elif self.pass_num == 6:
            for _ in range(5):
                for i in range(len(self.tags)):
                    for j in range(i+1, len(self.tags)):
                        d = self.tags[i]['pos'].dist(self.tags[j]['pos'])
                        if d < self.spacing and d > 0.001:
                            push = (self.tags[i]['pos'] - self.tags[j]['pos']).norm() * (self.spacing - d) * 0.5
                            self.tags[i]['pos'] = self.tags[i]['pos'] + push
                            self.tags[j]['pos'] = self.tags[j]['pos'] - push
            result = 'PASS 7: {}\nClashes: {} -> {}'.format(name, before, self.clashes())
        else:
            score = LayoutScorer(self.tags, self.spacing).score()[0]
            result = 'PASS 8: COMPLETE\n\nInitial: {}/100\nFinal: {}/100\nClashes: {}'.format(
                self.init_score, score, self.clashes())
        self.pass_num += 1
        return result
    def apply(self):
        from Autodesk.Revit.DB import XYZ
        for t in self.tags:
            try: t['tag'].TagHeadPosition = XYZ(t['pos'].x, t['pos'].y, t['z'])
            except: pass
    def reset(self):
        for t in self.tags: t['pos'] = t['orig'].copy()
        self.pass_num = 0


# ─────────────────────────────────────────────────────────────────────────────
# SELECTION ENGINE
# ─────────────────────────────────────────────────────────────────────────────
class SelectionEngine:
    COMMON_CATEGORIES = {
        'lights':   'OST_LightingFixtures',
        'elec':     'OST_ElectricalEquipment',
        'mech':     'OST_MechanicalEquipment',
        'plumb':    'OST_PlumbingFixtures',
        'air':      'OST_DuctTerminal',
        'furn':     'OST_Furniture',
        'doors':    'OST_Doors',
        'windows':  'OST_Windows',
        'rooms':    'OST_Rooms',
        'walls':    'OST_Walls',
        'floors':   'OST_Floors',
        'ceilings': 'OST_Ceilings',
        'columns':  'OST_Columns',
        'pipes':    'OST_PipeCurves',
        'ducts':    'OST_DuctCurves',
        'conduit':  'OST_Conduit',
        'cable':    'OST_CableTray',
        'sprinkler':'OST_Sprinklers',
        'casework': 'OST_Casework',
        'generic':  'OST_GenericModel',
    }

    def __init__(self, doc, uidoc, view):
        self.doc, self.uidoc, self.view = doc, uidoc, view
        self.memory = [[], [], []]
        self._tagged_cache = None
        self._cat_cache = None
        self._shared_params_cache = None

    def _bic(self, key):
        from Autodesk.Revit.DB import BuiltInCategory
        name = self.COMMON_CATEGORIES.get(key, key)
        return getattr(BuiltInCategory, name, None)

    def _get_bb_center(self, elem):
        try:
            bb = elem.get_BoundingBox(self.view)
            if bb: return Vec2((bb.Min.X+bb.Max.X)/2, (bb.Min.Y+bb.Max.Y)/2)
        except: pass
        return None

    def _get_bb_z(self, elem):
        try:
            bb = elem.get_BoundingBox(self.view)
            if bb: return (bb.Min.Z + bb.Max.Z) / 2
        except: pass
        return None

    def _set_selection(self, elems):
        if elems:
            ids = List[ElementId]([e.Id for e in elems if e])
            self.uidoc.Selection.SetElementIds(ids)
        return len(elems) if elems else 0

    def _get_selection(self):
        try: return [self.doc.GetElement(eid) for eid in self.uidoc.Selection.GetElementIds()]
        except: return []

    def _get_tags(self):
        try:
            return list(FilteredElementCollector(self.doc, self.view.Id)
                        .OfClass(IndependentTag).ToElements())
        except: return []

    def _tagged_elem_ids(self):
        if self._tagged_cache is None:
            self._tagged_cache = set()
            for tag in self._get_tags():
                try:
                    for r in tag.GetTaggedLocalElements():
                        self._tagged_cache.add(r.Id.IntegerValue)
                except: pass
        return self._tagged_cache

    def get_all_categories_in_view(self):
        if self._cat_cache: return self._cat_cache
        categories = {}
        try:
            collector = FilteredElementCollector(self.doc, self.view.Id).WhereElementIsNotElementType()
            for elem in collector:
                try:
                    cat = elem.Category
                    if cat and cat.Id.IntegerValue > 0:
                        name = cat.Name
                        if name and name not in categories: categories[name] = cat.Id
                except: pass
        except: pass
        self._cat_cache = categories
        return categories

    def get_all_model_categories(self):
        categories = {}
        try:
            for cat in self.doc.Settings.Categories:
                try:
                    if cat.CategoryType == CategoryType.Model and cat.AllowsBoundParameters:
                        categories[cat.Name] = cat.Id
                except: pass
        except: pass
        return categories

    def by_category_id(self, cat_id):
        try:
            elems = list(FilteredElementCollector(self.doc, self.view.Id)
                         .OfCategoryId(cat_id).WhereElementIsNotElementType().ToElements())
            return self._set_selection(elems)
        except: return 0

    def by_category_name(self, cat_name):
        all_cats = self.get_all_categories_in_view()
        if cat_name in all_cats: return self.by_category_id(all_cats[cat_name])
        return 0

    def _collect_cat(self, bic):
        try:
            return list(FilteredElementCollector(self.doc, self.view.Id)
                        .OfCategory(bic).WhereElementIsNotElementType().ToElements())
        except: return []

    def _collect_cat_all(self):
        all_elems, seen = [], set()
        from Autodesk.Revit.DB import BuiltInCategory
        for key in self.COMMON_CATEGORIES:
            bic = self._bic(key)
            if bic is None: continue
            for e in self._collect_cat(bic):
                if e.Id.IntegerValue not in seen:
                    all_elems.append(e); seen.add(e.Id.IntegerValue)
        return all_elems

    def _collect_all_in_view(self):
        try:
            return list(FilteredElementCollector(self.doc, self.view.Id)
                        .WhereElementIsNotElementType().ToElements())
        except: return []

    def get_shared_parameters(self):
        if self._shared_params_cache: return self._shared_params_cache
        params = {}
        try:
            binding_map = self.doc.ParameterBindings
            iterator = binding_map.ForwardIterator()
            while iterator.MoveNext():
                try:
                    defn = iterator.Key
                    if defn: params[defn.Name] = defn
                except: pass
        except: pass
        self._shared_params_cache = params
        return params

    def get_parameter_values(self, param_name, sample_size=100):
        values, count = set(), 0
        for elem in self._collect_all_in_view():
            if count >= sample_size: break
            try:
                p = elem.LookupParameter(param_name)
                if p:
                    val = p.AsString() or p.AsValueString()
                    if val: values.add(val); count += 1
            except: pass
        return sorted(values)

    def by_shared_parameter(self, param_name, value=None, operator='equals'):
        result = []
        for e in self._collect_all_in_view():
            try:
                p = e.LookupParameter(param_name)
                if not p: continue
                p_val = p.AsString() or p.AsValueString() or ''
                if value is None:
                    if p_val: result.append(e)
                elif operator == 'equals':
                    if p_val == value: result.append(e)
                elif operator == 'contains':
                    if value.lower() in p_val.lower(): result.append(e)
                elif operator == 'startswith':
                    if p_val.lower().startswith(value.lower()): result.append(e)
                elif operator == 'endswith':
                    if p_val.lower().endswith(value.lower()): result.append(e)
                elif operator == 'empty':
                    if not p_val or p_val.strip() == '': result.append(e)
                elif operator == 'notempty':
                    if p_val and p_val.strip() != '': result.append(e)
                elif operator == 'greater':
                    try:
                        if float(p_val) > float(value): result.append(e)
                    except: pass
                elif operator == 'less':
                    try:
                        if float(p_val) < float(value): result.append(e)
                    except: pass
            except: pass
        return self._set_selection(result)

    def smart_predict(self):
        sel = self._get_selection()
        if not sel:
            tagged = self._tagged_elem_ids()
            cat_counts = {}
            for cat_name, cat_id in self.get_all_categories_in_view().items():
                try:
                    elems = list(FilteredElementCollector(self.doc, self.view.Id)
                                 .OfCategoryId(cat_id).WhereElementIsNotElementType().ToElements())
                    untagged = [e for e in elems if e.Id.IntegerValue not in tagged]
                    if untagged: cat_counts[cat_name] = (len(untagged), cat_id)
                except: pass
            if cat_counts:
                best = max(cat_counts, key=lambda x: cat_counts[x][0])
                count, cat_id = cat_counts[best]
                elems = [e for e in FilteredElementCollector(self.doc, self.view.Id)
                         .OfCategoryId(cat_id).WhereElementIsNotElementType().ToElements()
                         if e.Id.IntegerValue not in tagged]
                return self._set_selection(elems), 'Predicted: {} untagged {}'.format(count, best)
            return 0, 'Nothing to predict'
        analysis = self._analyze_selection(sel)
        if analysis['type_dominance'] > 0.7:
            n, msg = self.select_similar(); return n, msg
        elif analysis['spatial_cluster'] > 0.6:
            n, msg = self.select_cluster(); return n, msg
        elif analysis['param_pattern']:
            param, val = analysis['param_pattern']
            n = self.by_shared_parameter(param, val)
            return n, 'Predicted by {}: {}'.format(param, n)
        else:
            return self.select_similar()

    def _analyze_selection(self, sel):
        analysis = {'type_dominance': 0, 'spatial_cluster': 0, 'param_pattern': None}
        if not sel: return analysis
        type_counts = {}
        for e in sel:
            try:
                tid = e.GetTypeId().IntegerValue
                type_counts[tid] = type_counts.get(tid, 0) + 1
            except: pass
        if type_counts:
            analysis['type_dominance'] = max(type_counts.values()) / len(sel)
        centers = [self._get_bb_center(e) for e in sel if self._get_bb_center(e)]
        if len(centers) >= 3:
            avg_dist = sum(centers[i].dist(centers[j])
                          for i in range(len(centers)) for j in range(i+1, len(centers))) / max(1, len(centers)*(len(centers)-1)/2)
            all_centers = [self._get_bb_center(e) for e in self._collect_cat_all() if self._get_bb_center(e)]
            if all_centers:
                n = min(50, len(all_centers))
                global_avg = sum(all_centers[i].dist(all_centers[j])
                                for i in range(n) for j in range(i+1, n)) / max(1, n*(n-1)/2)
                if global_avg > 0:
                    analysis['spatial_cluster'] = min(1.0, global_avg / max(avg_dist, 0.1))
        common_params = {}
        for e in sel[:10]:
            try:
                for p in e.Parameters:
                    if p.Definition:
                        val = p.AsString() or p.AsValueString()
                        if val:
                            key = (p.Definition.Name, val)
                            common_params[key] = common_params.get(key, 0) + 1
            except: pass
        if common_params:
            best_param = max(common_params, key=common_params.get)
            if common_params[best_param] >= len(sel) * 0.8:
                analysis['param_pattern'] = best_param
        return analysis

    def select_similar(self):
        sel = self._get_selection()
        if not sel: return 0, 'Select element first'
        type_ids = set()
        for e in sel:
            try: type_ids.add(e.GetTypeId().IntegerValue)
            except: pass
        similar = [e for e in self._collect_all_in_view()
                   if e.GetTypeId().IntegerValue in type_ids]
        return self._set_selection(similar), 'Selected {} similar'.format(len(similar))

    def select_chain(self):
        sel = self._get_selection()
        if not sel: return 0, 'Select MEP element first'
        from Autodesk.Revit.DB import BuiltInCategory
        mep_bics = [BuiltInCategory.OST_MechanicalEquipment, BuiltInCategory.OST_DuctTerminal,
                    BuiltInCategory.OST_ElectricalEquipment, BuiltInCategory.OST_LightingFixtures,
                    BuiltInCategory.OST_PipeCurves, BuiltInCategory.OST_DuctCurves]
        all_mep = []
        for bic in mep_bics: all_mep.extend(self._collect_cat(bic))
        if not all_mep: return 0, 'No MEP elements'
        elem_map, positions = {}, []
        for e in all_mep:
            c = self._get_bb_center(e)
            if c: elem_map[len(positions)] = e; positions.append(c)
        sel_indices = set()
        sel_centers = [self._get_bb_center(e) for e in sel if self._get_bb_center(e)]
        for i, pos in enumerate(positions):
            for sc in sel_centers:
                if pos.dist(sc) < 0.1: sel_indices.add(i); break
        if not sel_indices: return 0, 'Selection not in MEP set'
        graph = ProximityGraph(positions, threshold=3.0); graph.build()
        connected = set()
        for idx in sel_indices: connected.update(graph.find_component_of(idx))
        result = [elem_map[i] for i in connected if i in elem_map]
        return self._set_selection(result), 'Chain: {} connected'.format(len(result))

    def select_cluster(self):
        sel = self._get_selection()
        if not sel: return 0, 'Select element first'
        all_elems = self._collect_cat_all()
        elem_map, positions = {}, []
        for e in all_elems:
            c = self._get_bb_center(e)
            if c: elem_map[len(positions)] = e; positions.append(c)
        if not positions: return 0, 'No elements'
        sel_center = Vec2()
        sel_count = 0
        for e in sel:
            c = self._get_bb_center(e)
            if c: sel_center = sel_center + c; sel_count += 1
        if sel_count > 0: sel_center = sel_center / sel_count
        dbscan = DBSCAN(positions, eps=2.0, min_pts=2)
        clusters = dbscan.run()
        best_cluster, min_dist = -1, float('inf')
        for label, indices in clusters.items():
            if label == -1: continue
            cc = Vec2(sum(positions[i].x for i in indices)/len(indices),
                      sum(positions[i].y for i in indices)/len(indices))
            d = sel_center.dist(cc)
            if d < min_dist: min_dist = d; best_cluster = label
        if best_cluster == -1: return 0, 'No cluster found'
        result = [elem_map[i] for i in clusters[best_cluster] if i in elem_map]
        return self._set_selection(result), 'Cluster: {} elements'.format(len(result))

    def select_pattern(self):
        sel = self._get_selection()
        if len(sel) < 2: return 0, 'Select 2+ elements'
        centers = [self._get_bb_center(e) for e in sel if self._get_bb_center(e)]
        if len(centers) < 2: return 0, 'No positions'
        detector = GridDetector(centers, tolerance=0.3)
        x_grid, y_grid = detector.detect()
        if not x_grid and not y_grid: return 0, 'No grid pattern'
        type_ids = set(e.GetTypeId().IntegerValue for e in sel)
        all_elems = self._collect_cat_all()
        elem_map, all_positions = {}, []
        for e in all_elems:
            try:
                if e.GetTypeId().IntegerValue not in type_ids: continue
                c = self._get_bb_center(e)
                if c: elem_map[len(all_positions)] = e; all_positions.append(c)
            except: pass
        if not all_positions: return 0, 'No matching elements'
        full_detector = GridDetector(all_positions, tolerance=0.3)
        on_grid = full_detector.on_grid(x_grid, y_grid)
        result = [elem_map[i] for i in on_grid if i in elem_map]
        return self._set_selection(result), 'Grid pattern: {} elements'.format(len(result))

    def select_boundary(self, edge='all'):
        all_elems = self._collect_cat_all()
        elem_map, positions = {}, []
        for e in all_elems:
            c = self._get_bb_center(e)
            if c: elem_map[len(positions)] = e; positions.append(c)
        if not positions: return 0, 'No elements'
        detector = BoundaryDetector(positions, margin_pct=0.15)
        edges = detector.find_edge_elements()
        if edge == 'all':
            indices = set()
            for e in ['top', 'bottom', 'left', 'right']: indices.update(edges[e])
        else:
            indices = set(edges.get(edge, []))
        result = [elem_map[i] for i in indices if i in elem_map]
        return self._set_selection(result), '{} edge: {} elements'.format(edge.title(), len(result))

    def by_category(self, cat_name):
        bic = self._bic(cat_name)
        if bic:
            elems = self._collect_cat(bic)
            return self._set_selection(elems)
        return self.by_category_name(cat_name)

    def untagged(self, cat_name=None):
        tagged = self._tagged_elem_ids()
        if cat_name:
            bic = self._bic(cat_name)
            elems = self._collect_cat(bic) if bic else []
        else:
            elems = self._collect_cat_all()
        result = [e for e in elems if e.Id.IntegerValue not in tagged]
        return self._set_selection(result)

    def tagged(self):
        tagged = self._tagged_elem_ids()
        elems = [e for e in self._collect_cat_all() if e.Id.IntegerValue in tagged]
        return self._set_selection(elems)

    def near_selection(self, radius=5.0):
        sel = self._get_selection()
        if not sel: return 0
        centers = [self._get_bb_center(e) for e in sel if self._get_bb_center(e)]
        if not centers: return 0
        near = []
        for e in self._collect_cat_all():
            c = self._get_bb_center(e)
            if c and any(c.dist(sc) <= radius for sc in centers): near.append(e)
        return self._set_selection(near)

    def in_room(self):
        sel = self._get_selection()
        if not sel: return 0
        rooms = set()
        for e in sel:
            try:
                room = e.Room
                if room: rooms.add(room.Id.IntegerValue)
            except: pass
        if not rooms: return 0
        result = [e for e in self._collect_cat_all()
                  if hasattr(e, 'Room') and e.Room and e.Room.Id.IntegerValue in rooms]
        return self._set_selection(result)

    def same_level(self):
        sel = self._get_selection()
        if not sel: return 0
        levels = set()
        for e in sel:
            try:
                lid = e.LevelId
                if lid and lid != ElementId.InvalidElementId: levels.add(lid.IntegerValue)
            except: pass
        if not levels: return 0
        result = [e for e in self._collect_cat_all()
                  if hasattr(e, 'LevelId') and e.LevelId.IntegerValue in levels]
        return self._set_selection(result)

    def quadrant(self, q):
        all_elems = self._collect_cat_all()
        centers = [(e, self._get_bb_center(e)) for e in all_elems]
        centers = [(e, c) for e, c in centers if c]
        if not centers: return 0
        xs = [c.x for _, c in centers]; ys = [c.y for _, c in centers]
        mx = (min(xs)+max(xs))/2; my = (min(ys)+max(ys))/2
        result = []
        for e, c in centers:
            if q == 1 and c.x < mx and c.y >= my: result.append(e)
            elif q == 2 and c.x >= mx and c.y >= my: result.append(e)
            elif q == 3 and c.x < mx and c.y < my: result.append(e)
            elif q == 4 and c.x >= mx and c.y < my: result.append(e)
        return self._set_selection(result)

    def by_param_value(self, param_name, value=None):
        return self.by_shared_parameter(param_name, value, 'equals')

    def empty_param(self, param_name='Mark'):
        return self.by_shared_parameter(param_name, None, 'empty')

    def invert_selection(self):
        current_ids = set(e.Id.IntegerValue for e in self._get_selection())
        inverted = [e for e in self._collect_cat_all() if e.Id.IntegerValue not in current_ids]
        return self._set_selection(inverted)

    def select_tags_of_elements(self):
        sel = self._get_selection()
        if not sel: return 0
        sel_ids = set(e.Id.IntegerValue for e in sel)
        result = []
        for tag in self._get_tags():
            try:
                for r in tag.GetTaggedLocalElements():
                    if r.Id.IntegerValue in sel_ids: result.append(tag); break
            except: pass
        return self._set_selection(result)

    def select_elements_of_tags(self):
        sel = self._get_selection()
        tags = [e for e in sel if isinstance(e, IndependentTag)]
        if not tags: return 0
        result = []
        for tag in tags:
            try:
                for r in tag.GetTaggedLocalElements(): result.append(r)
            except: pass
        return self._set_selection(result)

    def save_to_memory(self, slot):
        self.memory[slot] = [e.Id for e in self._get_selection()]
        return len(self.memory[slot])

    def load_from_memory(self, slot):
        if self.memory[slot]:
            elems = [self.doc.GetElement(eid) for eid in self.memory[slot]]
            return self._set_selection([e for e in elems if e])
        return 0

    def swap_memory(self):
        self.memory[0], self.memory[1] = self.memory[1], self.memory[0]
