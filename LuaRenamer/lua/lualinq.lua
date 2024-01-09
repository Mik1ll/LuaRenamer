-- Modified for LuaRenamer to add annotations, bug fixes, and add orderby function

-- LuaLinq - http://code.google.com/p/lualinq/
-- ------------------------------------------------------------------------
-- Copyright (c) 2012, Marco Mastropaolo (Xanathar)
-- All rights reserved.
--
-- Redistribution and use in source and binary forms, with or without modification,
-- are permitted provided that the following conditions are met:
--
--  o Redistributions of source code must retain the above copyright notice,
-- 	  this list of conditions and the following disclaimer.
--  o Redistributions in binary form must reproduce the above copyright notice,
-- 	  this list of conditions and the following disclaimer in the documentation
-- 	  and/or other materials provided with the distribution.
--  o Neither the name of Marco Mastropaolo nor the names of its contributors
-- 	  may be used to endorse or promote products derived from this software
-- 	  without specific prior written permission.
--
-- THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
-- ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
-- WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
-- IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT,
-- INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING,
-- BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
-- DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
-- LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE
-- OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED
-- OF THE POSSIBILITY OF SUCH DAMAGE.
-- ------------------------------------------------------------------------

local LIB_VERSION_TEXT = "1.5.1"
local LIB_VERSION = 151

-- support lua 5.2+
local unpack = table.unpack

-- ============================================================
-- DEBUG TRACER
-- ============================================================

-- how much log information is printed: 3 => debug, 2 => info, 1 => only warning and errors, 0 => only errors, -1 => silent
local LOG_LEVEL = 1

-- prefix for the printed logs
local LOG_PREFIX = "LuaLinq: "

function linqSetLogLevel(level)
	LOG_LEVEL = level;
end

local function logdebug(txt)
	if (3 <= LOG_LEVEL) then
		_ENV.logdebug(LOG_PREFIX .. txt)
	end
end

local function log(txt)
	if (2 <= LOG_LEVEL) then
		_ENV.log(LOG_PREFIX .. txt)
	end
end

local function logwarn(txt)
	if (1 <= LOG_LEVEL) then
		_ENV.logwarn(LOG_PREFIX .. txt)
	end
end

local function logerror(txt)
	if (0 <= LOG_LEVEL) then
		_ENV.logerror(LOG_PREFIX .. txt)
	end
end

-- ============================================================
-- CLASS
-- ============================================================

---@class Linq
---@field private m_Data table
---@field private chained_compare? fun(a, b):integer
local Linq = {}
Linq.__index = Linq

-- Creates a linq data structure from an array without copying the data for efficiency
---@private
function Linq.new(method, collection)
	local instance = setmetatable({}, Linq)

	instance.m_Data = collection

	logdebug("after " .. method .. " => " .. #instance.m_Data .. " items : " .. instance:dump())

	return instance
end

-- Returns dumped data
function Linq:dump()
	local items = #self.m_Data
	local dumpdata = "q{ "

	for i = 1, 3 do
		if (i <= items) then
			if (i ~= 1) then
				dumpdata = dumpdata .. ", "
			end
			dumpdata = dumpdata .. tostring(self.m_Data[i])
		end
	end

	if (items > 3) then
		dumpdata = dumpdata .. ", ..." .. items .. " }"
	else
		dumpdata = dumpdata .. " }"
	end
	return dumpdata
end

-- ============================================================
-- GENERATORS
-- ============================================================

-- Tries to autodetect input type and uses the appropriate from method
---@return Linq
function from(auto)
	if (auto == nil) then
		return fromNothing()
	elseif (type(auto) == "function") then
		return fromIterator(auto)
	elseif (type(auto) == "table") then
		if (getmetatable(auto) == Linq) then
			return auto
		elseif (auto[1] == nil) then
			return fromDictionary(auto)
		elseif (type(auto[1]) == "function") then
			return fromIteratorsArray(auto)
		else
			return fromArrayInstance(auto)
		end
	end
	return fromNothing()
end

-- Creates a linq data structure from an array without copying the data for efficiency
function fromArrayInstance(collection)
	return Linq.new("fromArrayInstance", collection)
end

-- Creates a linq data structure from an array copying the data first (so that changes in the original
-- table do not reflect here)
function fromArray(array)
	local collection = {}
	for k, v in ipairs(array) do
		table.insert(collection, v)
	end
	return Linq.new("fromArray", collection)
end

-- Creates a linq data structure from a dictionary (table with non-consecutive-integer keys)
function fromDictionary(dictionary)
	local collection = {}

	for k, v in pairs(dictionary) do
		local kvp = {}
		kvp.key = k
		kvp.value = v

		table.insert(collection, kvp)
	end
	return Linq.new("fromDictionary", collection)
end

-- Creates a linq data structure from an iterator returning single items
function fromIterator(iterator)
	local collection = {}

	for s in iterator do
		table.insert(collection, s)
	end
	return Linq.new("fromIterator", collection)
end

-- Creates a linq data structure from an array of iterators each returning single items
function fromIteratorsArray(iteratorArray)
	local collection = {}

	for _, iterator in ipairs(iteratorArray) do
		for s in iterator do
			table.insert(collection, s)
		end
	end
	return Linq.new("fromIteratorsArray", collection)
end

-- Creates a linq data structure from a table of keys, values ignored
function fromSet(set)
	local collection = {}

	for k, v in pairs(set) do
		table.insert(collection, k)
	end
	return Linq.new("fromIteratorsArray", collection)
end

-- Creates an empty linq data structure
function fromNothing()
	return Linq.new("fromNothing", {})
end

-- ============================================================
-- QUERY METHODS
-- ============================================================

-- Concatenates two collections together
---@param other table
---@return Linq
function Linq:concat(other)
	local result = {}
	other = from(other)

	for idx, value in ipairs(self.m_Data) do
		table.insert(result, value)
	end
	for idx, value in ipairs(other.m_Data) do
		table.insert(result, value)
	end
	return Linq.new(":concat", result)
end

-- Projects items returned by the selector function or the values of fields by name
---@param selector string|fun(value):any
function Linq:select(selector)
	local result = {}

	if (type(selector) == "function") then
		for idx, value in ipairs(self.m_Data) do
			local newvalue = selector(value)
			if (newvalue ~= nil) then
				table.insert(result, newvalue)
			end
		end
	elseif (type(selector) == "string") then
		for idx, value in ipairs(self.m_Data) do
			local newvalue = value[selector]
			if (newvalue ~= nil) then
				table.insert(result, newvalue)
			end
		end
	else
		error("select called with unknown predicate type");
	end
	return Linq.new(":select", result)
end

-- Returns merged projection of the arrays returned by the selector function
---@param selector fun(value):table
function Linq:selectMany(selector)
	local result = {}

	for idx, value in ipairs(self.m_Data) do
		local newvalue = selector(value)
		if (newvalue ~= nil) then
			for ii, vv in ipairs(newvalue) do
				if (vv ~= nil) then
					table.insert(result, vv)
				end
			end
		end
	end
	return Linq.new(":selectMany", result)
end

-- Returns a linq data structure where only items for whose the predicate has returned true are included
---@param predicate string|fun(value, ...):boolean @ String: matches the table key. Function: optional additional arguments can be passed
---@param refvalue? any @ String predicate: matches the value in the dictionary. Function predicate: Passed as additional argument
---@param ... any @ String predicate: additional values to match. Function predicate: passed as additional arguments
function Linq:where(predicate, refvalue, ...)
	local result = {}

	if (type(predicate) == "function") then
		for idx, value in ipairs(self.m_Data) do
			if (predicate(value, refvalue, from({ ... }):toTuple())) then
				table.insert(result, value)
			end
		end
	elseif (type(predicate) == "string") then
		local refvals = { ... }

		if (#refvals > 0) then
			table.insert(refvals, refvalue);
			return self:intersectionby(predicate, refvals);
		elseif (refvalue ~= nil) then
			for idx, value in ipairs(self.m_Data) do
				if (value[predicate] == refvalue) then
					table.insert(result, value)
				end
			end
		else
			for idx, value in ipairs(self.m_Data) do
				if (value[predicate] ~= nil) then
					table.insert(result, value)
				end
			end
		end
	else
		error("where called with unknown predicate type");
	end
	return Linq.new(":where", result)
end

-- Returns a linq data structure where only items for whose the predicate has returned true are included, indexed version
---@param predicate fun(index:integer, value:any):boolean
function Linq:whereIndex(predicate)
	local result = {}

	for idx, value in ipairs(self.m_Data) do
		if (predicate(idx, value)) then
			table.insert(result, value)
		end
	end
	return Linq.new(":whereIndex", result)
end

-- Return a linq data structure with at most the first howmany elements
---@param howmany integer
---@return Linq
function Linq:take(howmany)
	return self:whereIndex(function(i, v) return i <= howmany; end)
end

-- Return a linq data structure skipping the first howmany elements
---@param howmany integer
---@return Linq
function Linq:skip(howmany)
	return self:whereIndex(function(i, v) return i > howmany; end)
end

-- Zips two collections together, using the specified join function
---@param other table
---@param joiner? fun(a, b):any
function Linq:zip(other, joiner)
	other = from(other)
	joiner = joiner or function(a, b) return { a, b } end

	local thismax = #self.m_Data
	local thatmax = #other.m_Data
	local result = {}

	if (thatmax < thismax) then thismax = thatmax; end

	for i = 1, thismax do
		result[i] = joiner(self.m_Data[i], other.m_Data[i]);
	end
	return Linq.new(":zip", result)
end

---@param array table
---@param comparator? fun(a, b):integer|boolean
local function insertionsort(array, comparator)
	comp = function(a, b)
		if (comparator == nil) then
			return a < b
		else
			local res = comparator(a, b)
			return res == true or res < 0
		end
	end
	for i = 2, #array do
		j = i
		while j > 1 and not comp(array[j - 1], array[j]) do
			array[j], array[j - 1] = array[j - 1], array[j]
			j = j - 1
		end
	end
end

local function compare(a, b)
	if (a < b) then
		return -1
	elseif (a > b) then
		return 1
	else
		return 0
	end
end

---Returns ordered items according to a selector and comparer (must return -1, 0, 1)
---@param selector string|fun(value):any @ Key selector
---@param comparator? fun(a, b):integer @ Key comparer, -1(a<b), 0(a==b), 1(a>b)
function Linq:orderby(selector, comparator)
	comparator = comparator or compare
	if (type(selector) == "string") then
		local key = selector
		selector = function(value)
			return value[key]
		end
	end
	local result = {}
	for idx, value in ipairs(self.m_Data) do
		result[idx] = value
	end
	local function compfunc(a, b)
		local ares = selector(a)
		local bres = selector(b)
		return comparator(ares, bres)
	end
	insertionsort(result, compfunc)
	result = Linq.new(":orderby", result)
	result.chained_compare = compfunc
	return result
end

Linq.orderBy = Linq.orderby

---Returns ordered items in descending order according to a selector and comparer (must return -1, 0, 1)
---@param selector string|fun(value):any @ Key selector
---@param comparator? fun(a, b):integer @ Key comparer, -1(a<b), 0(a==b), 1(a>b)
function Linq:orderbyDescending(selector, comparator)
	comparator = comparator or compare
	newcomp = function(a, b)
		return -comparator(a, b)
	end
	return self:orderby(selector, newcomp)
end

Linq.orderByDescending = Linq.orderbyDescending
Linq.orderbyDesc = Linq.orderbyDescending
Linq.orderByDesc = Linq.orderbyDescending

---Used after orderBy or another thenBy, adds a lower priority ordering
---@param selector string|fun(value):any @ Key selector
---@param comparator? fun(a, b):integer @ Key comparer, -1(a<b), 0(a==b), 1(a>b)
function Linq:thenby(selector, comparator)
	if (self.chained_compare == nil) then
		error("thenby called without orderby first")
	end
	comparator = comparator or compare
	if (type(selector) == "string") then
		local key = selector
		selector = function(value)
			return value[key]
		end
	end
	local function compfunc(a, b)
		local baseres = self.chained_compare(a, b)
		if (baseres ~= 0) then
			return baseres
		end
		local ares = selector(a)
		local bres = selector(b)
		return comparator(ares, bres)
	end
	insertionsort(self.m_Data, compfunc)
	local result = Linq.new(":thenby", self.m_Data)
	result.chained_compare = compfunc
	return result
end

Linq.thenBy = Linq.thenby

---Used after orderBy or another thenBy, adds a lower priority ordering in descending order
---@param selector string|fun(value):any @ Key selector
---@param comparator? fun(a, b):integer @ Key comparer, -1(a<b), 0(a==b), 1(a>b)
function Linq:thenbyDescending(selector, comparator)
	comparator = comparator or compare
	newcomp = function(a, b)
		return -comparator(a, b)
	end
	return self:thenby(selector, newcomp)
end

Linq.thenByDescending = Linq.thenbyDescending
Linq.thenbyDesc = Linq.thenbyDescending
Linq.thenByDesc = Linq.thenbyDescending

-- Returns only distinct items, using an optional comparator
---@param comparator? fun(a, b):boolean
function Linq:distinct(comparator)
	local result = {}

	for idx, value in ipairs(self.m_Data) do
		local found = false

		for _, value2 in ipairs(result) do
			if (comparator == nil) then
				if (value == value2) then found = true; end
			else
				if (comparator(value, value2)) then found = true; end
			end
		end

		if (not found) then
			table.insert(result, value)
		end
	end
	return Linq.new(":distinct", result)
end

-- Returns the union of two collections, using an optional comparator
---@param other table
---@param comparator? fun(a, b):boolean
---@return Linq
function Linq:union(other, comparator)
	return self:concat(from(other)):distinct(comparator)
end

-- Returns the difference of two collections, using an optional comparator
---@param other table
---@param comparator? fun(a, b):boolean
---@return Linq
function Linq:except(other, comparator)
	other = from(other)
	return self:where(function(v) return not other:contains(v, comparator) end)
end

-- Returns the intersection of two collections, using an optional comparator
---@param other table
---@param comparator? fun(a, b):boolean
---@return Linq
function Linq:intersection(other, comparator)
	other = from(other)
	return self:where(function(v) return other:contains(v, comparator) end)
end

Linq.intersect = Linq.intersection

-- Returns the collection excluding items in the other collection using a property accessor
---@param property any
---@param other table
---@return Linq
function Linq:exceptby(property, other)
	other = from(other)
	return self:where(function(v) return not other:contains(v[property]) end)
end

Linq.exceptBy = Linq.exceptby

-- Returns the collection only including items in the other collection using a property accessor
---@param property any
---@param other table
---@return Linq
function Linq:intersectionby(property, other)
	other = from(other)
	return self:where(function(v) return other:contains(v[property]) end)
end

Linq.intersectionBy = Linq.intersectionby
Linq.intersectby = Linq.intersectionby
Linq.intersectBy = Linq.intersectionby

-- ============================================================
-- CONVERSION METHODS
-- ============================================================

-- Converts the collection to an iterator
function Linq:toIterator()
	local i = 0
	local n = #self.m_Data
	return function()
		i = i + 1
		if i <= n then return self.m_Data[i] end
	end
end

-- Converts the collection to an array
---@return table
function Linq:toArray()
	return self.m_Data
end

-- Converts the collection to a table using a selector functions which returns key and value for each item
---@return table
function Linq:toDictionary(keyValueSelector)
	local result = {}

	for idx, value in ipairs(self.m_Data) do
		local key, value = keyValueSelector(value)
		if (key ~= nil) then
			result[key] = value
		end
	end
	return result
end

-- Converts the lualinq struct to a tuple
---@return any ...
function Linq:toTuple()
	return unpack(self.m_Data)
end

-- ============================================================
-- TERMINATING METHODS
-- ============================================================

-- Return the first item or default if no items in the colelction
---@param default? any
---@return any
function Linq:first(default)
	if (#self.m_Data > 0) then
		return self.m_Data[1]
	else
		return default
	end
end

-- Return the last item or default if no items in the colelction
---@param default? any
---@return any
function Linq:last(default)
	if (#self.m_Data > 0) then
		return self.m_Data[#self.m_Data]
	else
		return default
	end
end

-- Returns true if any item satisfies the predicate. If predicate is null, it returns true if the collection has at least one item.
---@param predicate? fun(value):boolean
---@return boolean
function Linq:any(predicate)
	if (predicate == nil) then return #self.m_Data > 0; end

	for idx, value in ipairs(self.m_Data) do
		if (predicate(value)) then
			return true
		end
	end
	return false
end

-- Returns true if all items satisfy the predicate. If predicate is null, it returns true if the collection is empty.
---@param predicate? fun(value):boolean
---@return boolean
function Linq:all(predicate)
	if (predicate == nil) then return #self.m_Data == 0; end

	for idx, value in ipairs(self.m_Data) do
		if (not predicate(value)) then
			return false
		end
	end
	return true
end

-- Returns the number of items satisfying the predicate. If predicate is null, it returns the number of items in the collection.
---@param predicate? fun(value):boolean
---@return integer
function Linq:count(predicate)
	if (predicate == nil) then return #self.m_Data; end

	local result = 0

	for idx, value in ipairs(self.m_Data) do
		if (predicate(value)) then
			result = result + 1
		end
	end
	return result
end

-- Returns a random item in the collection, or default if no items are present
function Linq:random(default)
	if (#self.m_Data == 0) then return default; end
	return self.m_Data[math.random(1, #self.m_Data)]
end

-- Returns true if the collection contains the specified item
---@generic T
---@param item T
---@param comparator? fun(a: T, b: T):boolean
---@return boolean
function Linq:contains(item, comparator)
	for idx, value in ipairs(self.m_Data) do
		if (comparator == nil) then
			if (value == item) then return true; end
		else
			if (comparator(value, item)) then return true; end
		end
	end
	return false
end

-- Calls the action for each item in the collection. Action's first parameter is the value, any more are provided by var args.
-- If the action is a string, it calls that method on each value plus additional provided arguments
---@param action string|fun(value:any, ...:any)
---@param ... any
---@return Linq
function Linq:foreach(action, ...)
	if (type(action) == "function") then
		for idx, value in ipairs(self.m_Data) do
			action(value, from({ ... }):toTuple())
		end
	elseif (type(action) == "string") then
		for idx, value in ipairs(self.m_Data) do
			value[action](value, from({ ... }):toTuple())
		end
	else
		error("foreach called with unknown action type");
	end
	return self
end

Linq.each = Linq.foreach

-- Calls the accumulator for each item in the collection. Accumulator takes 2 parameters: value and the previous result of
-- the accumulator itself (firstvalue for the first call) and returns a new result.
---@generic T
---@param accumulator fun(value, result:T):T
---@param firstvalue T
---@return T
function Linq:map(accumulator, firstvalue)
	local result = firstvalue

	for idx, value in ipairs(self.m_Data) do
		result = accumulator(value, result)
	end
	return result
end

-- Calls the accumulator for each item in the collection. Accumulator takes 3 parameters: value, the previous result of
-- the accumulator itself (nil on first call) and the previous associated-result of the accumulator(firstvalue for the first call)
-- and returns a new result and a new associated-result.
---@generic T1
---@generic T2
---@param accumulator fun(value, lastresult:T1, lastvalue:T2):T1, T2
---@param firstvalue? T2
---@return T1
function Linq:xmap(accumulator, firstvalue)
	local result = nil
	local lastval = firstvalue

	for idx, value in ipairs(self.m_Data) do
		result, lastval = accumulator(value, result, lastval)
	end
	return result
end

-- Returns the max of a collection. Selector is called with values and should return a number. Can be nil if collection is of numbers.
---@param selector? fun(value):number
---@return number
function Linq:max(selector)
	if (selector == nil) then
		selector = function(n) return n; end
	end
	return self:xmap(
		function(v, r, l)
			local res = selector(v);
			if (l == nil or res > l) then return v, res; else return r, l; end
			;
		end, nil)
end

-- Returns the min of a collection. Selector is called with values and should return a number. Can be nil if collection is of numbers.
---@param selector? fun(value):number
---@return number
function Linq:min(selector)
	if (selector == nil) then
		selector = function(n) return n; end
	end
	return self:xmap(
		function(v, r, l)
			local res = selector(v);
			if (l == nil or res < l) then return v, res; else return r, l; end
			;
		end, nil)
end

-- Returns the sum of a collection. Selector is called with values and should return a number. Can be nil if collection is of numbers.
---@param selector? fun(value):number
---@return number
function Linq:sum(selector)
	if (selector == nil) then
		selector = function(n) return n; end
	end
	return self:map(function(n, r)
		r = r + selector(n);
		return r;
	end, 0)
end

-- Returns the average of a collection. Selector is called with values and should return a number. Can be nil if collection is of numbers.
---@param selector? fun(value):number
---@return number
function Linq:average(selector)
	local count = self:count()
	if (count > 0) then
		return self:sum(selector) / count
	else
		return 0
	end
end
